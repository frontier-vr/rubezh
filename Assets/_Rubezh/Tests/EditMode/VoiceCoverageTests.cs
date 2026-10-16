using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Rubezh.Tests.EditMode
{
    // Озвучка сценария (YG-13): у каждой команды, подсказки и причины стопа из script.ru.json
    // есть WAV в Audio/Voice/, фон тира лежит в Audio/Ambience/.
    public class VoiceCoverageTests
    {
        private const string Voice = "Assets/_Rubezh/Audio/Voice/";

        [System.Serializable] private class Command { public string voice; }
        [System.Serializable] private class Hint { public string id; }
        [System.Serializable] private class FailReason { public string violation; }
        [System.Serializable] private class Script { public Command[] commands; public Hint[] hints; public FailReason[] failReasons; }

        [Test]
        public void EveryScriptLine_HasVoiceFile()
        {
            var s = JsonUtility.FromJson<Script>(File.ReadAllText("Assets/_Rubezh/Data/Hints/script.ru.json"));
            Assert.AreEqual(13, s.commands.Length);
            foreach (var c in s.commands) Assert.IsTrue(File.Exists(Voice + c.voice + ".wav"), c.voice);
            foreach (var h in s.hints) Assert.IsTrue(File.Exists(Voice + h.id + ".wav"), h.id);
            foreach (var f in s.failReasons) Assert.IsTrue(File.Exists(Voice + "fail_" + f.violation + ".wav"), f.violation);
        }

        [Test]
        public void Ambience_IsThere()
        {
            Assert.IsTrue(File.Exists("Assets/_Rubezh/Audio/Ambience/Range_RoomTone.wav"));
        }
    }
}
