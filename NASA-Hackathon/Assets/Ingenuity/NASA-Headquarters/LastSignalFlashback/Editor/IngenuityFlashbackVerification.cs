#if UNITY_EDITOR
using System;
using System.Linq;
using LastSignal.IngenuityFlashback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace LastSignal.IngenuityFlashback.EditorTools
{
    /// <summary>Runs the assembly, snap, reset, systems-check, and handoff path in a temporary scene.</summary>
    public static class IngenuityFlashbackVerification
    {
        [MenuItem("Last Signal/Ingenuity Flashback/Verify Assembly Flow")]
        public static void VerifyAssemblyFlow()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene verificationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject root = null;
            int handoffCount = 0;
            Action<string> handoffListener = _ => handoffCount++;

            try
            {
                VerifySavedPlayerRig(previousScene);
                root = new GameObject("Temporary Ingenuity Flashback Verification");
                SceneManager.MoveGameObjectToScene(root, verificationScene);
                IngenuityFlashbackSceneController controller = root.AddComponent<IngenuityFlashbackSceneController>();
                var savedFacility = IngenuityFacilityLayout.BuildEnvironment(root.transform, null);
                var preview = new GameObject("Saved edit-mode assembly preview");
                preview.transform.SetParent(savedFacility.transform, false);
                savedFacility.SetAssemblyPreview(preview);
                GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IngenuityFacilityLayout.LanderModelPath);
                Require(modelPrefab != null, "The Ingenuity helicopter model must be available for assembly verification.");
                controller.InitializeForEditorVerification(modelPrefab, null);

                Require(controller.GetComponentsInChildren<IngenuityFacilityEnvironment>(true).Length == 1,
                    "Runtime must reuse the saved room instead of creating a duplicate.");
                Require(!preview.activeSelf, "The edit-mode mockup must be hidden when interactions initialize.");
                Require(controller.OpportunityModelAnchor.childCount == 1, "The imported rover must be assigned under the dedicated model anchor.");
                GameObject modelInstance = controller.OpportunityModelAnchor.GetChild(0).gameObject;
                Require(modelInstance.activeInHierarchy, "The imported rover chassis must be visible before assembly begins.");
                Transform legacyRootProxy = modelInstance.transform.Find("_root");
                Renderer rootProxyRenderer = legacyRootProxy != null ? legacyRootProxy.GetComponent<Renderer>() : null;
                Require(rootProxyRenderer == null || !rootProxyRenderer.enabled,
                    "The imported _root proxy mesh must stay hidden, matching the working Legacy rover setup.");
                Require(Mathf.Abs(modelInstance.transform.localScale.x - 1f) < .001f,
                    "The detailed rover FBX should use its authored meter scale.");
                Require(Quaternion.Angle(modelInstance.transform.localRotation, Quaternion.Euler(0f, 180f, 0f)) < .1f,
                    "The detailed rover FBX should face the assembly area like the Legacy and Arrival scenes.");
                Transform[][] modelSources = IngenuityFacilityLayout.FindRoverComponentSources(modelInstance.transform);
                Require(modelSources != null, "The helicopter must expose separate rotor, solar panel, and leg groups.");
                for (int i = 0; i < modelSources.Length; i++)
                    foreach (Transform source in modelSources[i])
                        Require(!source.gameObject.activeSelf, "Detached rover components should begin on their trays, not attached to the chassis.");
                Require(controller.AssemblyParts.Count == 3, "Expected exactly three assembly parts.");
                Transform componentBenchGroup = savedFacility.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(item => item != null && item.name == "Component preparation bench");
                Renderer componentBench = componentBenchGroup != null
                    ? componentBenchGroup.GetComponentsInChildren<Renderer>(true)
                        .FirstOrDefault(renderer => renderer != null && renderer.name == "Laminate bench surface")
                    : null;
                Require(componentBench != null, "The detached rover components need a saved preparation bench.");
                Bounds[] componentBounds = new Bounds[controller.AssemblyParts.Count];
                Vector3[] originalTrayPositions = new Vector3[controller.AssemblyParts.Count];
                for (int i = 0; i < originalTrayPositions.Length; i++)
                {
                    originalTrayPositions[i] = controller.AssemblyParts[i].transform.position;
                    componentBounds[i] = IngenuityFacilityLayout.GetBounds(
                        controller.AssemblyParts[i].GetComponentsInChildren<Renderer>(true));
                    Bounds deskBounds = componentBench.bounds;
                    Bounds componentBoundsOnDesk = componentBounds[i];
                    const float deskEdgeClearance = .03f;
                    Debug.Log($"[Last Signal] Bench fit {controller.AssemblyParts[i].Kind}: component center={componentBoundsOnDesk.center} " +
                              $"size={componentBoundsOnDesk.size}; desk center={deskBounds.center} size={deskBounds.size}.");
                    Require(componentBoundsOnDesk.min.x >= deskBounds.min.x + deskEdgeClearance &&
                            componentBoundsOnDesk.max.x <= deskBounds.max.x - deskEdgeClearance &&
                            componentBoundsOnDesk.min.z >= deskBounds.min.z + deskEdgeClearance &&
                            componentBoundsOnDesk.max.z <= deskBounds.max.z - deskEdgeClearance,
                        "The detached " + controller.AssemblyParts[i].Kind + " must fit fully on the component bench.");
                    Require(componentBoundsOnDesk.min.y >= deskBounds.max.y - .02f,
                        "The detached " + controller.AssemblyParts[i].Kind + " must rest above the component bench.");
                }
                for (int i = 0; i < componentBounds.Length; i++)
                    for (int j = i + 1; j < componentBounds.Length; j++)
                        Require(!componentBounds[i].Intersects(componentBounds[j]),
                            "The detached component groups must not overlap on the bench.");
                Require(controller.PlayerCamera != null && controller.PlayerCamera.CompareTag("MainCamera"),
                    "The player rig must provide the active MainCamera for this scene.");
                foreach (IngenuityAssemblyPart part in controller.AssemblyParts)
                {
                    XRGrabInteractable grab = part.GetComponent<XRGrabInteractable>();
                    Require(grab != null && grab.enabled, "Every tray component must be enabled for the shared XR grab interactors.");
                    Require(grab.colliders.Contains(part.GetComponent<Collider>()),
                        "Each tray component's collider must be registered with its XR grab interactable.");
                    Require(part.GetComponentsInChildren<Renderer>(true).Length > 0, "Every tray component must use visible geometry from the imported FBX.");
                    Require(AreRenderersEnabled(part.gameObject, true), "Detached model parts should be visible on the bench before assembly.");
                }
                Require(controller.SystemsCheckButton.GetComponent<XRSimpleInteractable>() != null,
                    "The systems-check button must be selectable by controller rays.");
                Require(controller.ReadyButton.GetComponent<XRSimpleInteractable>() != null,
                    "The launch button must be selectable by controller rays.");
                Require(controller.AssemblyProgress == 0, "Assembly should start at zero of three.");
                Require(!controller.SystemsCheckPassed, "Systems check should start incomplete.");
                Require(!controller.IsLaunchReady, "Launch should start locked.");
                Require(!controller.SystemsCheckButton.Interactable, "Systems Check should start locked.");
                Require(!controller.ReadyButton.Interactable, "Ready for Launch should start locked.");
                Require(!controller.RunSystemsCheck(), "An incomplete rover must reject the systems check.");
                Require(!controller.RequestLaunchHandoff(), "An incomplete rover must reject launch.");

                IngenuityAssemblyPart firstPart = controller.AssemblyParts[0];
                firstPart.transform.position = firstPart.SnapTarget.position + Vector3.up * 2f;
                Require(!firstPart.TrySnapAtCurrentPose(), "A part outside the snap radius must not snap.");
                Require(!firstPart.IsSnapped, "A rejected snap must leave the part unsnapped.");
                firstPart.ResetPart();

                for (int i = 0; i < controller.AssemblyParts.Count; i++)
                {
                    IngenuityAssemblyPart part = controller.AssemblyParts[i];
                    GameObject partHoldAnchor = new GameObject("Verification controller hand hold anchor " + i);
                    partHoldAnchor.transform.SetParent(root.transform, true);
                    partHoldAnchor.transform.position = part.transform.position;
                    Require(part.BeginGrab(partHoldAnchor.transform),
                        "Controller pickup should work for " + part.Kind + ".");
                    Require(part.transform.parent == partHoldAnchor.transform,
                        "Picking up " + part.Kind + " should attach it to the controller hold anchor.");
                    part.transform.SetPositionAndRotation(
                        part.SnapTarget.position + Vector3.up * 0.16f, part.SnapTarget.rotation);
                    Require(part.ReleaseFromGrab(),
                        "Releasing " + part.Kind + " inside the snap radius should snap it.");
                    UnityEngine.Object.DestroyImmediate(partHoldAnchor);
                    Require(part.IsSnapped, "The snapped " + part.Kind + " should report its snapped state.");
                    Require(part.transform.parent == part.SnapTarget,
                        "The snapped " + part.Kind + " should be parented to its socket.");
                    Require(!part.SnapTarget.Find("Mount guide").gameObject.activeSelf,
                        "A fitted component should hide its temporary alignment guide.");
                    Require(ModelSourcesAreActive(modelSources[(int)part.Kind], true),
                        "Snapping " + part.Kind + " should enable its original FBX group on the rover.");
                    Require(AreRenderersEnabled(part.gameObject, false),
                        "The snapped tray copy of " + part.Kind + " should hide as the original rover geometry turns on.");
                }
                Require(controller.AssemblyProgress == 3, "All three successful snaps should advance progress to three.");
                Renderer assemblySurface = savedFacility.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer != null && renderer.name == "Machined assembly surface");
                Require(assemblySurface != null, "The central rover fixture should expose its assembly surface for fit checks.");
                Bounds roverBounds = IngenuityFacilityLayout.GetBounds(modelInstance.GetComponentsInChildren<Renderer>(true));
                Bounds surfaceBounds = assemblySurface.bounds;
                const float platformEdgeClearance = .06f;
                Debug.Log($"[Last Signal] Assembled rover bounds center={roverBounds.center} size={roverBounds.size}; " +
                          $"surface center={surfaceBounds.center} size={surfaceBounds.size}.");
                // Ingenuity's solar arrays reach far past the lander body, as on the real spacecraft. So the landing
                // legs must stand on the podium, and the whole spacecraft must stay clear of the bench and console.
                Bounds legBounds = IngenuityFacilityLayout.GetBounds(modelInstance.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.StartsWith("leg_", StringComparison.Ordinal)).ToArray());
                Debug.Log($"[Last Signal] Landing legs bounds center={legBounds.center} size={legBounds.size}.");
                Require(legBounds.size != Vector3.zero &&
                        legBounds.min.x >= surfaceBounds.min.x + platformEdgeClearance &&
                        legBounds.max.x <= surfaceBounds.max.x - platformEdgeClearance &&
                        legBounds.min.z >= surfaceBounds.min.z + platformEdgeClearance &&
                        legBounds.max.z <= surfaceBounds.max.z - platformEdgeClearance,
                    "The lander's legs must stand inside the podium surface.");
                float consoleWestEdge = IngenuityFacilityLayout.ConsoleCenter.x - 1.05f;
                Require(roverBounds.min.x >= componentBench.bounds.max.x + .1f && roverBounds.max.x <= consoleWestEdge - .1f,
                    "The assembled lander, arrays included, must stay clear of the component bench and the systems console.");
                Require(roverBounds.min.y >= surfaceBounds.max.y - .03f,
                    "The reassembled rover must sit above the podium surface instead of passing through it.");
                Require(controller.SystemsCheckButton.Interactable, "Systems Check should unlock after all three snaps.");
                Require(!controller.IsLaunchReady, "Snapping all parts alone must not unlock launch.");

                IngenuityFlashbackSceneController.LaunchHandoffRequested += handoffListener;
                try
                {
                    Require(controller.RunSystemsCheck(), "Systems check should pass after all three snaps.");
                    Require(controller.SystemsCheckPassed, "Systems check pass state was not recorded.");
                    Require(controller.IsLaunchReady, "A passed systems check should unlock launch.");
                    Require(controller.ReadyButton.Interactable, "Ready for Launch should unlock after systems check.");
                    Require(modelInstance.activeInHierarchy, "The completed imported rover must remain visible after the systems check.");
                    for (int i = 0; i < modelSources.Length; i++)
                        foreach (Transform source in modelSources[i])
                            Require(source.gameObject.activeSelf, "Systems check should restore the imported components onto the rover.");
                    Require(controller.RequestLaunchHandoff(), "A ready rover should emit the launch handoff.");
                    Require(handoffCount == 1, "Expected one launch handoff event.");

                    controller.ResetAssembly();
                    Require(controller.AssemblyProgress == 0, "Reset should clear all snapped parts.");
                    Require(!controller.SystemsCheckPassed, "Reset should clear the systems-check state.");
                    Require(!controller.IsLaunchReady, "Reset should re-lock launch.");
                    Require(!controller.SystemsCheckButton.Interactable, "Reset should re-lock Systems Check.");
                    Require(!controller.ReadyButton.Interactable, "Reset should re-lock Ready for Launch.");
                    Require(!controller.RequestLaunchHandoff(), "Launch handoff must remain blocked after reset.");
                    for (int i = 0; i < modelSources.Length; i++)
                    {
                        Require(ModelSourcesAreActive(modelSources[i], false), "Reset should return original rover parts to the tray copies.");
                    }
                    for (int i = 0; i < controller.AssemblyParts.Count; i++)
                    {
                        var part = controller.AssemblyParts[i];
                        Require(Vector3.Distance(part.transform.position, originalTrayPositions[i]) < .001f,
                            "Reset must return each component to its own bench tray.");
                        Require(AreRenderersEnabled(part.gameObject, true),
                            "Reset must show the detached component copy on its bench tray again.");
                        Require(part.SnapTarget.Find("Mount guide").gameObject.activeSelf,
                            "Reset must restore each numbered alignment guide.");
                    }
                }
                finally
                {
                    IngenuityFlashbackSceneController.LaunchHandoffRequested -= handoffListener;
                }

                Debug.Log("[Last Signal] Ingenuity flashback verification passed: saved-room reuse, three controller pickups and production-path snaps, completed rover visibility, systems check, locked launch, handoff, and bench reset.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                if (verificationScene.IsValid() && verificationScene.isLoaded)
                    EditorSceneManager.CloseScene(verificationScene, true);
            }
        }

        private static void VerifySavedPlayerRig(Scene scene)
        {
            GameObject player = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                if (sceneRoot.name.StartsWith("Flashback Player - shared tracked XR rig"))
                {
                    player = sceneRoot;
                }
            Require(player != null, "The saved flashback scene must contain its shared XR player rig.");
            Camera camera = player.GetComponentInChildren<Camera>(true);
            Require(camera != null && camera.CompareTag("MainCamera"), "The saved player rig must have a MainCamera.");

            MonoBehaviour[] components = player.GetComponentsInChildren<MonoBehaviour>(true);
            Require(HasComponentType(components,"XRInteractionManager"), "The saved player rig needs the project's XR Interaction Manager.");
            Require(HasComponentType(components,"XRRayInteractor"), "The saved player rig needs controller ray interactors for the physical displays.");
            Require(HasComponentType(components,"XRDirectInteractor"), "The saved player rig needs direct interactors for grabbing rover parts.");
            Require(HasComponentType(components,"DynamicMoveProvider") || HasComponentType(components,"ContinuousMoveProvider"),
                "The saved player rig needs its comfortable continuous-movement provider.");
            Require(HasComponentType(components,"SnapTurnProvider"), "The saved player rig needs its snap-turn provider.");
        }

        private static bool HasComponentType(MonoBehaviour[] components,string typeName)
        {
            foreach (MonoBehaviour component in components)
                if (component != null && component.GetType().Name.Contains(typeName)) return true;
            return false;
        }

        private static bool ModelSourcesAreActive(Transform[] sources,bool expected)
        {
            if (sources == null || sources.Length == 0) return false;
            foreach (Transform source in sources)
                if (source == null || source.gameObject.activeSelf != expected) return false;
            return true;
        }

        private static bool AreRenderersEnabled(GameObject root,bool expectedEnabled)
        {
            if (root == null) return false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            bool foundVisiblePartGeometry = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) return false;
                if (IsImportedPlaceholder(renderer))
                {
                    if (renderer.enabled) return false;
                    continue;
                }
                foundVisiblePartGeometry = true;
                if (renderer.enabled != expectedEnabled) return false;
            }
            return foundVisiblePartGeometry;
        }

        private static bool IsImportedPlaceholder(Renderer renderer)
        {
            // The Opportunity FBX had leftover Blender boxes named Cube/_Hemi. The Ingenuity lander has none:
            // its "Cube..." meshes are the solar array panels and must stay visible.
            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Ingenuity flashback verification failed: " + message);
        }
    }
}
#endif
