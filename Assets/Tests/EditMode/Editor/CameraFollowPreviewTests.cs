using NUnit.Framework;
using RelicKeeper.Editor.Combat;
using Unity.Cinemachine;
using Unity.Cinemachine.Editor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CameraFollowPreviewTests
    {
        [TestCase(false)] [TestCase(true)]
        public void PreviewRestoresExactOriginalSettings_IncludingBeforeCinemachineHotSave(bool hotSave)
        {
            var host = new GameObject("Camera follow preview test");
            try
            {
                host.AddComponent<CinemachineCamera>(); var composer = host.AddComponent<CinemachinePositionComposer>();
                composer.Damping = new Vector3(.4f, .7f, .3f);
                composer.Lookahead = new LookaheadSettings { Enabled = false, Time = .6f, Smoothing = 7, IgnoreY = false };
                Vector3 offset = composer.TargetOffset = new Vector3(1, 2, 3);
                using (var preview = new CameraFollowPreviewSession(composer))
                {
                    preview.Preview(.12f, .05f); Assert.That(preview.IsPreviewing, Is.True);
                    Assert.That(composer.Lookahead.IgnoreY, Is.True); Assert.That(composer.Damping.y, Is.Zero);
                    Assert.That(composer.Lookahead.Time, Is.EqualTo(.12f));
                    if (hotSave) SaveDuringPlay.OnHotSave?.Invoke(); else preview.Restore();
                    Assert.That(composer.Damping, Is.EqualTo(new Vector3(.4f, .7f, .3f)));
                    Assert.That(composer.Lookahead.Enabled, Is.False); Assert.That(composer.Lookahead.Time, Is.EqualTo(.6f));
                    Assert.That(composer.Lookahead.Smoothing, Is.EqualTo(7)); Assert.That(composer.Lookahead.IgnoreY, Is.False);
                    Assert.That(composer.TargetOffset, Is.EqualTo(offset));
                    preview.Preview(100, 100); Assert.That(composer.Lookahead.Time, Is.EqualTo(.2f));
                    Assert.That(composer.Damping.x, Is.EqualTo(.1f));
                }
                Assert.That(composer.Damping, Is.EqualTo(new Vector3(.4f, .7f, .3f)));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
