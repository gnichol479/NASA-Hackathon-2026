#if UNITY_EDITOR
using System;
using LastSignal.OpportunityFlashback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                root = new GameObject("Temporary Opportunity Flashback Verification");
                SceneManager.MoveGameObjectToScene(root, verificationScene);
                OpportunityFlashbackSceneController controller = root.AddComponent<OpportunityFlashbackSceneController>();
                controller.InitializeForEditorVerification();

                Require(controller.AssemblyParts.Count == 3, "Expected exactly three assembly parts.");
                Require(controller.AssemblyProgress == 0, "Assembly should start at zero of three.");
                Require(!controller.SystemsCheckPassed, "Systems check should start incomplete.");
                Require(!controller.IsLaunchReady, "Launch should start locked.");
                Require(!controller.SystemsCheckButton.Interactable, "Systems Check should start locked.");
                Require(!controller.ReadyButton.Interactable, "Ready for Launch should start locked.");

                OpportunityAssemblyPart firstPart = controller.AssemblyParts[0];
                firstPart.transform.position = firstPart.SnapTarget.position + Vector3.up * 2f;
                Require(!firstPart.TrySnapAtCurrentPose(), "A part outside the snap radius must not snap.");
                Require(!firstPart.IsSnapped, "A rejected snap must leave the part unsnapped.");
                firstPart.ResetPart();

                firstPart.transform.position = firstPart.SnapTarget.position + Vector3.up * 0.16f;
                Require(firstPart.ReleaseFromGrab(), "A part released inside the snap radius should snap.");
                Require(firstPart.IsSnapped, "The snapped part should report its snapped state.");
                Require(firstPart.transform.parent == firstPart.SnapTarget, "A snapped part should be parented to its socket.");

                for (int i = 1; i < controller.AssemblyParts.Count; i++)
                    Require(controller.AssemblyParts[i].VerifySnapAtTarget(), "Part " + i + " did not snap at its target.");
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
                    Require(controller.RequestLaunchHandoff(), "A ready rover should emit the launch handoff.");
                    Require(handoffCount == 1, "Expected one launch handoff event.");

                    controller.ResetAssembly();
                    Require(controller.AssemblyProgress == 0, "Reset should clear all snapped parts.");
                    Require(!controller.SystemsCheckPassed, "Reset should clear the systems-check state.");
                    Require(!controller.IsLaunchReady, "Reset should re-lock launch.");
                    Require(!controller.SystemsCheckButton.Interactable, "Reset should re-lock Systems Check.");
                    Require(!controller.ReadyButton.Interactable, "Reset should re-lock Ready for Launch.");
                    Require(!controller.RequestLaunchHandoff(), "Launch handoff must remain blocked after reset.");
                }
                finally
                {
                    OpportunityFlashbackSceneController.LaunchHandoffRequested -= handoffListener;
                }

                Debug.Log("[Last Signal] Opportunity flashback verification passed: progression, near/far snap, systems check, locked launch, handoff, and reset.");
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

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Opportunity flashback verification failed: " + message);
        }
    }
}
#endif
