using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Gameplay;

namespace KitchenChaos.EditorTools
{
    [InitializeOnLoad]
    public static partial class KitchenSmokeTest
    {
        private const string SessionKey = "KitchenChaos.CampaignSmoke.Active";
        private static readonly List<string> failures = new List<string>();
        private static int checks;
        private static bool finishing;
        private static GameManager gm;
        private static PlayerController player;
        private static CuttingCounter cutter;
        private static StoveCounter stove;
        private static PlateCounter plates;
        private static PrepCounter prep;
        private static DeliveryCounter delivery;

        static KitchenSmokeTest()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.update += WaitForKitchen;
            Application.logMessageReceived += CaptureError;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch is for a separate batch-mode Editor, not an open working scene.");
            SessionState.SetBool(SessionKey, true);
            SessionState.SetFloat(SessionKey + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("KitchenChaos").AddComponent<KitchenBuilder>();
            EditorApplication.update -= WaitForKitchen;
            EditorApplication.update += WaitForKitchen;
            Application.logMessageReceived -= CaptureError;
            Application.logMessageReceived += CaptureError;
            EditorApplication.isPlaying = true;
        }

        private static void WaitForKitchen()
        {
            if (finishing) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(SessionKey + ".Start", 0f) > 120f)
            {
                failures.Add("Timed out entering Play Mode.");
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying || GameManager.Instance == null || Time.frameCount < 2) return;
            EditorApplication.update -= WaitForKitchen;
            try
            {
                gm = GameManager.Instance;
                gm.SaveRecords = false;
                player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                cutter = UnityEngine.Object.FindAnyObjectByType<CuttingCounter>();
                stove = UnityEngine.Object.FindAnyObjectByType<StoveCounter>();
                plates = UnityEngine.Object.FindAnyObjectByType<PlateCounter>();
                prep = UnityEngine.Object.FindAnyObjectByType<PrepCounter>();
                delivery = UnityEngine.Object.FindAnyObjectByType<DeliveryCounter>();
                VerifySceneAndRules();
                VerifyEveryRecipe();
                VerifyProcessingAndPause();
                VerifyCampaign();
                VerifyUpgradeEffects();
                VerifyFeedback();
                gm.ReturnToMenu();
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
            }
            EditorApplication.delayCall += Finish;
        }

        private static void CaptureError(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failures.Add(type + ": " + message + "\n" + trace);
        }

        private static void Finish()
        {
            if (finishing) return;
            finishing = true;
            SessionState.SetBool(SessionKey, false);
            Application.logMessageReceived -= CaptureError;
            string result = "KITCHEN CAMPAIGN: " + checks + " checks, " + failures.Count + " failures\n";
            result += failures.Count == 0 ? "ALL PASS" : string.Join("\n", failures);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/campaign-smoke.txt", result);
            Debug.Log(result);
            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }

        public static void BuildPlaytest()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch-mode Editor.");
            Directory.CreateDirectory("Builds/Playtest");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/KitchenChaos.unity" },
                locationPathName = "Builds/Playtest/KitchenChaos.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Playtest build failed: " + report.summary.result);
            Debug.Log("KITCHEN PLAYTEST BUILD: " + report.summary.result);
        }

        private static void Expect(bool condition, string label)
        {
            checks++;
            if (!condition) failures.Add("FAIL: " + label);
        }
    }
}
