// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Forma.Tests
{
    [TestFixture]
    public sealed class SvgImageSourceTest
    {
        private const string ValidSvg = "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='16' viewBox='0 0 48 32'><defs><linearGradient id='g'/></defs><path fill='url(#g)' d='M0 0h48v32H0z'/></svg>";

        [Test]
        public void LoadsMemoryStreamAndFileWithStableIdentityAndMetadata()
        {
            var bytes = Encoding.UTF8.GetBytes(ValidSvg);
            var memory = SvgImageSource.FromMemory(bytes);
            using var stream = new MemoryStream(bytes);
            var streamed = SvgImageSource.FromStream(stream);
            var path = Path.Combine(Path.GetTempPath(), $"forma-svg-{Guid.NewGuid():N}.svg");
            try
            {
                File.WriteAllBytes(path, bytes);
                var file = SvgImageSource.FromFile(path);
                Assert.Multiple(() =>
                {
                    Assert.That(memory.IntrinsicSize, Is.EqualTo(new Vector2(24, 16)));
                    Assert.That(memory.ViewBox, Is.EqualTo(new RectangleF(0, 0, 48, 32)));
                    Assert.That(memory.PreserveAspectRatio, Is.EqualTo("xMidYMid meet"));
                    Assert.That(memory.ElementCount, Is.EqualTo(4));
                    Assert.That(memory.LocalReferenceCount, Is.EqualTo(1));
                    Assert.That(streamed.ContentIdentity, Is.EqualTo(memory.ContentIdentity));
                    Assert.That(file, Is.EqualTo(memory));
                    Assert.That(memory.ContentIdentity, Has.Length.EqualTo(64));
                });
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void CopiesSourceMemoryAndReturnsReadOnlyStreams()
        {
            var bytes = Encoding.UTF8.GetBytes(ValidSvg);
            var source = SvgImageSource.FromMemory(bytes);
            bytes[1] = (byte)'x';

            using var retained = source.OpenStream();
            using var reader = new StreamReader(retained, Encoding.UTF8);
            Assert.Multiple(() =>
            {
                Assert.That(reader.ReadToEnd(), Is.EqualTo(ValidSvg));
                Assert.That(retained.CanWrite, Is.False);
            });
        }

        [TestCase("<root/>", SvgLoadErrorCode.InvalidRoot)]
        [TestCase("<svg width='16' height='16'><script/></svg>", SvgLoadErrorCode.ForbiddenContent)]
        [TestCase("<svg width='16' height='16' onload='run()'/>", SvgLoadErrorCode.ForbiddenContent)]
        [TestCase("<svg width='16' height='16'><use href='https://example.com/a.svg#x'/></svg>", SvgLoadErrorCode.ExternalReference)]
        [TestCase("<svg width='16' height='16'><path fill='url(file:///tmp/a.svg#x)'/></svg>", SvgLoadErrorCode.ExternalReference)]
        [TestCase("<?xml-stylesheet href='theme.css'?><svg width='16' height='16'/>", SvgLoadErrorCode.ForbiddenContent)]
        [TestCase("<svg width='16' height='16'><style>@import url(theme.css);</style></svg>", SvgLoadErrorCode.ExternalReference)]
        [TestCase("<svg width='16' height='16'><animate/></svg>", SvgLoadErrorCode.ForbiddenContent)]
        [TestCase("<svg width='16' height='16'><filter/></svg>", SvgLoadErrorCode.UnsupportedFeature)]
        [TestCase("<svg width='16' height='16'><text>label</text></svg>", SvgLoadErrorCode.UnsupportedFeature)]
        [TestCase("<svg width='16' height='16'><font/></svg>", SvgLoadErrorCode.UnsupportedFeature)]
        [TestCase("<svg width='16' height='16'><style>@font-face { font-family: x; }</style></svg>", SvgLoadErrorCode.ExternalReference)]
        [TestCase("<svg width='16' height='16'><rect style='mix-blend-mode:multiply'/></svg>", SvgLoadErrorCode.UnsupportedFeature)]
        [TestCase("<svg width='16' height='16' color-profile='print'/>", SvgLoadErrorCode.UnsupportedFeature)]
        [TestCase("<svg width='16' height='16'><image href='data:image/png;base64,AA=='/></svg>", SvgLoadErrorCode.UnsupportedFeature)]
        [TestCase("<svg width='NaN' height='16'/>", SvgLoadErrorCode.InvalidDimensions)]
        [TestCase("<svg width='16' height='16'><path stroke-width='Infinity'/></svg>", SvgLoadErrorCode.InvalidDimensions)]
        [TestCase("<svg viewBox='0 0 0 16'/>", SvgLoadErrorCode.InvalidDimensions)]
        public void RejectsInvalidOrForbiddenSources(string xml, SvgLoadErrorCode expected)
        {
            var error = Assert.Throws<SvgLoadException>(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(xml)));
            Assert.That(error.Code, Is.EqualTo(expected));
        }

        [Test]
        public void RejectsDtdsAndMalformedXmlWithoutResolvingEntities()
        {
            var dtd = "<!DOCTYPE svg [<!ENTITY external SYSTEM 'file:///etc/passwd'>]><svg width='16' height='16'>&external;</svg>";
            var malformed = "<svg width='16' height='16'>";

            Assert.Multiple(() =>
            {
                Assert.That(
                    Assert.Throws<SvgLoadException>(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(dtd))).Code,
                    Is.EqualTo(SvgLoadErrorCode.InvalidXml));
                Assert.That(
                    Assert.Throws<SvgLoadException>(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(malformed))).Code,
                    Is.EqualTo(SvgLoadErrorCode.InvalidXml));
            });
        }

        [Test]
        public void EnforcesSourceElementDepthReferenceAndDimensionBudgets()
        {
            var sourceLimit = new SvgLoadOptions { MaximumSourceBytes = 8 };
            var elementLimit = new SvgLoadOptions { MaximumElements = 2 };
            var depthLimit = new SvgLoadOptions { MaximumDepth = 2 };
            var referenceLimit = new SvgLoadOptions { MaximumLocalReferences = 0 };
            var dimensionLimit = new SvgLoadOptions { MaximumDimension = 15 };
            var attributeLimit = new SvgLoadOptions { MaximumAttributes = 2 };
            var textLimit = new SvgLoadOptions { MaximumTextBytes = 3 };
            var areaLimit = new SvgLoadOptions { MaximumPixelArea = 15 };

            Assert.Multiple(() =>
            {
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(ValidSvg), sourceLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.SourceTooLarge));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(ValidSvg), elementLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes("<svg width='1' height='1'><g><path/></g></svg>"), depthLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(ValidSvg), referenceLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes("<svg width='16' height='1'/>"), dimensionLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes("<svg width='1' height='1' viewBox='0 0 1 1'/>"), attributeLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes("<svg width='1' height='1'><style>abcd</style></svg>"), textLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes("<svg width='4' height='4'/>"), areaLimit), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
            });
        }

        [Test]
        public void RejectsDuplicateIdsAndCyclicLocalReferences()
        {
            var duplicate = "<svg width='1' height='1'><g id='a'/><g id='a'/></svg>";
            var cycle = "<svg width='1' height='1'><g id='a' fill='url(#b)'/><g id='b' fill='url(#a)'/></svg>";

            Assert.Multiple(() =>
            {
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(duplicate)), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.InvalidXml));
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(cycle)), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.ForbiddenContent));
            });
        }

        [Test]
        public void GeneratedMalformedAndDepthCorpusFailsWithBoundedSvgErrors()
        {
            var timer = Stopwatch.StartNew();

            for (var cut = 1; cut < ValidSvg.Length; cut += 7)
            {
                var truncated = ValidSvg.Substring(0, cut);
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(truncated)), Throws.TypeOf<SvgLoadException>());
            }

            for (var depth = 2; depth <= 20; depth++)
            {
                var xml = "<svg width='1' height='1'>" + string.Concat(Enumerable.Repeat("<g>", depth)) +
                    string.Concat(Enumerable.Repeat("</g>", depth)) + "</svg>";
                var options = new SvgLoadOptions { MaximumDepth = depth };
                Assert.That(() => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(xml), options), Throws.TypeOf<SvgLoadException>().With.Property(nameof(SvgLoadException.Code)).EqualTo(SvgLoadErrorCode.BudgetExceeded));
            }

            timer.Stop();
            Assert.That(timer.Elapsed.TotalMilliseconds, Is.LessThan(5000),
                $"Malformed/adversarial corpus must complete within 5000 ms; took {timer.Elapsed.TotalMilliseconds:0.0} ms.");
        }

        [Test]
        public void RejectsOversizedNonSeekableStreamsBeforeReadingPastTheLimit()
        {
            var bytes = Enumerable.Repeat((byte)'x', 33).ToArray();
            using var source = new NonSeekableStream(bytes);
            var error = Assert.Throws<SvgLoadException>(() => SvgImageSource.FromStream(source, new SvgLoadOptions { MaximumSourceBytes = 32 }));
            Assert.That(error.Code, Is.EqualTo(SvgLoadErrorCode.SourceTooLarge));
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            internal NonSeekableStream(byte[] source) : base(source) { }
            public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        }
    }
}