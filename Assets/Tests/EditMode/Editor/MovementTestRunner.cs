using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public static class MovementTestRunner
    {
        private static TestRunnerApi _runner;

        [MenuItem("Tools/Relic Keeper/Run Movement Tests")]
        public static void Run()
        {
            _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            _runner.RegisterCallbacks(new Results());
            _runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { "RelicKeeper.Tests.EditMode.PlayerMovementTests", "RelicKeeper.Tests.EditMode.SkillAssetRegressionTests" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = Path.GetFullPath("Library/MovementTestResults.xml");
                TestRunnerApi.SaveResultToFile(result, path);
                Debug.Log($"Movement tests: {result.PassCount} passed, {result.FailCount} failed. {path}");
                Object.DestroyImmediate(_runner);
            }
        }
    }
}
