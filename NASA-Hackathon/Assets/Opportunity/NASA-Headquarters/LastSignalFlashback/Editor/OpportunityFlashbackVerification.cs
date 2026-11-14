#if UNITY_EDITOR
using System;
using LastSignal.OpportunityFlashback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace LastSignal.OpportunityFlashback.EditorTools
{
    /// <summary>Runs the assembly, snap, reset, systems-check, and handoff path in a temporary scene.</summary>
    public static class OpportunityFlashbackVerification
    {
        [MenuItem("Last Signal/Opportunity Flashback/Verify Assembly Flow")]
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
                root = new GameObject("Temporary Opportunity Flashback Verification");
                SceneManager.MoveGameObjectToScene(root, verificationScene);
                OpportunityFlashbackSceneController controller = root.AddComponent<OpportunityFlashbackSceneController>();
                var savedFacility = OpportunityFacilityLayout.BuildEnvironment(root.transform, null);
                var preview = new GameObject("Saved edit-mode assembly preview");
                preview.transform.SetParent(savedFacility.transform, false);
                savedFacility.SetAssemblyPreview(preview);
                GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Opportunity/Models/Mars Exploration Rover - Spirit and Opportunity.fbx");
                Require(modelPrefab != null, "The updated Opportunity FBX must be available for assembly verification.");
                controller.InitializeForEditorVerification(modelPrefab);

                Require(controller.GetComponentsInChildren<OpportunityFacilityEnvironment>(true).Length == 1,
                    "Runtime must reuse the saved room instead of creating a duplicate.");
                Require(!preview.activeSelf, "The edit-mode mockup must be hidden when interactions initialize.");
                Require(controller.OpportunityModelAnchor.childCount == 1, "The imported rover must be assigned under the dedicated model anchor.");
                GameObject modelInstance = controller.OpportunityModelAnchor.GetChild(0).gameObject;
                Require(modelInstance.activeInHierarchy, "The imported rover chassis must be visible before assembly begins.");
                Transform[][] modelSources = OpportunityFacilityLayout.FindRoverComponentSources(modelInstance.transform);
                Require(modelSources != null, "The FBX must expose separate solar arrays, camera mast, and instrument arm groups.");
                for (int i = 0; i < modelSources.Length; i++)
                    foreach (Transform source in modelSources[i])
                        Require(!source.gameObject.activeSelf, "Detached rover components should begin on their trays, not attached to the chassis.");
                Require(controller.AssemblyParts.Count == 3, "Expected exactly three assembly parts.");
                Vector3[] originalTrayPositions = new Vector3[controller.AssemblyParts.Count];
                for (int i = 0; i < originalTrayPositions.Length; i++)
                    originalTrayPositions[i] = controller.AssemblyParts[i].transform.position;
                Require(controller.PlayerCamera != null && controller.PlayerCamera.CompareTag("MainCamera"),
                    "The player rig must provide the active MainCamera for this scene.");
                foreach (OpportunityAssemblyPart part in controller.AssemblyParts)
                {
                    Require(part.GetComponent<XRGrabInteractable>() != null, "Every tray component must be grabbable by the shared XR interactors.");
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

                OpportunityAssemblyPart firstPart = controller.AssemblyParts[0];
                firstPart.transform.position = firstPart.SnapTarget.position + Vector3.up * 2f;
                Require(!firstPart.TrySnapAtCurrentPose(), "A part outside the snap radius must not snap.");
                Require(!firstPart.IsSnapped, "A rejected snap must leave the part unsnapped.");
                firstPart.ResetPart();

                firstPart.transform.position = firstPart.SnapTarget.position + Vector3.up * 0.16f;
                Require(firstPart.ReleaseFromGrab(), "A part released inside the snap radius should snap.");
                Require(firstPart.IsSnapped, "The snapped part should report its snapped state.");
                Require(firstPart.transform.parent == firstPart.SnapTarget, "A snapped part should be parented to its socket.");
                Require(!firstPart.SnapTarget.Find("Mount guide").gameObject.activeSelf,
                    "A fitted component should hide its temporary alignment guide.");
                Require(ModelSourcesAreActive(modelSources[(int)firstPart.Kind], true),
                    "Snapping a part should immediately enable its original FBX group on the rover.");
                Require(AreRenderersEnabled(firstPart.gameObject, false),
                    "After snapping, the temporary tray copy should hide while the original model group is visible.");

                for (int i = 1; i < controller.AssemblyParts.Count; i++)
                {
                    OpportunityAssemblyPart part = controller.AssemblyParts[i];
                    Require(part.VerifySnapAtTarget(), "Part " + i + " did not snap at its target.");
                    Require(ModelSourcesAreActive(modelSources[(int)part.Kind], true),
                        "Each snapped original FBX group should turn on before the systems check.");
                    Require(AreRenderersEnabled(part.gameObject, false),
                        "The snapped tray copy should hide as the original rover geometry turns on.");
                }
                Require(controller.AssemblyProgress == 3, "All three successful snaps should advance progress to three.");
                Require(controller.SystemsCheckButton.Interactable, "Systems Check should unlock after all three snaps.");
                Require(!controller.IsLaunchReady, "Snapping all parts alone must not unlock launch.");

                OpportunityFlashbackSceneController.LaunchHandoffRequested += handoffListener;
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
                    OpportunityFlashbackSceneController.LaunchHandoffRequested -= handoffListener;
                }

                Debug.Log("[Last Signal] Opportunity flashback verification passed: saved-room reuse, progression, near/far snap, completed rover visibility, systems check, locked launch, handoff, and bench reset.");
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
            foreach (Renderer renderer in renderers)
                if (renderer == null || renderer.enabled != expectedEnabled) return false;
            return true;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Opportunity flashback verification failed: " + message);
        }
    }
}
#endif
