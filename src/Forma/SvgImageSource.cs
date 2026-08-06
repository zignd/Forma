// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Forma
{
    /// <summary>Classifies deterministic SVG source validation failures.</summary>
    public enum SvgLoadErrorCode
    {
        EmptySource,
        SourceTooLarge,
        InvalidXml,
        InvalidRoot,
        InvalidDimensions,
        BudgetExceeded,
        ForbiddenContent,
        ExternalReference,
        UnsupportedFeature,
    }

    /// <summary>Reports a bounded SVG source validation failure.</summary>
    public sealed class SvgLoadException : IOException
    {
        internal SvgLoadException(SvgLoadErrorCode code, string message, Exception innerException = null)
            : base(message, innerException) => Code = code;

        /// <summary>Gets the stable validation error category.</summary>
        public SvgLoadErrorCode Code { get; }
    }

    /// <summary>Configures finite limits applied before an SVG reaches the raster backend.</summary>
    public sealed class SvgLoadOptions
    {
        public const int DefaultMaximumSourceBytes = 4 * 1024 * 1024;
        public const int DefaultMaximumElements = 16 * 1024;
        public const int DefaultMaximumAttributes = 64 * 1024;
        public const int DefaultMaximumTextBytes = 1024 * 1024;
        public const int DefaultMaximumDepth = 128;
        public const int DefaultMaximumLocalReferences = 16 * 1024;
        public const int DefaultMaximumDimension = 16 * 1024;
        public const long DefaultMaximumPixelArea = 64L * 1024 * 1024;

        public int MaximumSourceBytes { get; set; } = DefaultMaximumSourceBytes;
        public int MaximumElements { get; set; } = DefaultMaximumElements;
        public int MaximumAttributes { get; set; } = DefaultMaximumAttributes;
        public int MaximumTextBytes { get; set; } = DefaultMaximumTextBytes;
        public int MaximumDepth { get; set; } = DefaultMaximumDepth;
        public int MaximumLocalReferences { get; set; } = DefaultMaximumLocalReferences;
        public int MaximumDimension { get; set; } = DefaultMaximumDimension;
        public long MaximumPixelArea { get; set; } = DefaultMaximumPixelArea;

        internal SvgLoadLimits Snapshot()
        {
            if (MaximumSourceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumSourceBytes));
            if (MaximumElements <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumElements));
            if (MaximumAttributes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumAttributes));
            if (MaximumTextBytes < 0) throw new ArgumentOutOfRangeException(nameof(MaximumTextBytes));
            if (MaximumDepth <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumDepth));
            if (MaximumLocalReferences < 0) throw new ArgumentOutOfRangeException(nameof(MaximumLocalReferences));
            if (MaximumDimension <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumDimension));
            if (MaximumPixelArea <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumPixelArea));
            return new SvgLoadLimits(this);
        }
    }

    internal readonly struct SvgLoadLimits
    {
        internal SvgLoadLimits(SvgLoadOptions options)
        {
            MaximumSourceBytes = options.MaximumSourceBytes;
            MaximumElements = options.MaximumElements;
            MaximumAttributes = options.MaximumAttributes;
            MaximumTextBytes = options.MaximumTextBytes;
            MaximumDepth = options.MaximumDepth;
            MaximumLocalReferences = options.MaximumLocalReferences;
            MaximumDimension = options.MaximumDimension;
            MaximumPixelArea = options.MaximumPixelArea;
        }

        internal int MaximumSourceBytes { get; }
        internal int MaximumElements { get; }
        internal int MaximumAttributes { get; }
        internal int MaximumTextBytes { get; }
        internal int MaximumDepth { get; }
        internal int MaximumLocalReferences { get; }
        internal int MaximumDimension { get; }
        internal long MaximumPixelArea { get; }
    }

    /// <summary>Describes an immutable image source that can be rasterized at a requested physical size.</summary>
    public abstract class ScalableImageSource
    {
        /// <summary>Gets the source's logical intrinsic size.</summary>
        public abstract Vector2 IntrinsicSize { get; }
        /// <summary>Gets the stable identity used by scalable-image caches.</summary>
        public abstract string ContentIdentity { get; }
    }

    /// <summary>Stores an immutable, preflight-validated SVG source without graphics-device ownership.</summary>
    public sealed class SvgImageSource : ScalableImageSource, IEquatable<SvgImageSource>
    {
        private readonly byte[] _source;

        private SvgImageSource(byte[] source, SvgSourceMetadata metadata)
        {
            _source = source;
            IntrinsicSize = metadata.IntrinsicSize;
            ViewBox = metadata.ViewBox;
            PreserveAspectRatio = metadata.PreserveAspectRatio;
            ElementCount = metadata.ElementCount;
            LocalReferenceCount = metadata.LocalReferenceCount;
            ContentIdentity = Convert.ToHexString(SHA256.HashData(source));
        }

        public override Vector2 IntrinsicSize { get; }
        public RectangleF? ViewBox { get; }
        public string PreserveAspectRatio { get; }
        public int ElementCount { get; }
        public int LocalReferenceCount { get; }
        public int SourceLength => _source.Length;
        public override string ContentIdentity { get; }

        /// <summary>Loads and validates an SVG from a file, copying its bytes into the returned source.</summary>
        public static SvgImageSource FromFile(string path, SvgLoadOptions options = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            using var stream = File.OpenRead(path);
            return FromStream(stream, options);
        }

        /// <summary>Loads and validates an SVG from an assembly manifest resource.</summary>
        public static SvgImageSource FromManifestResource(Assembly assembly, string logicalName, SvgLoadOptions options = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrWhiteSpace(logicalName)) throw new ArgumentException("SVG resource name must not be empty.", nameof(logicalName));
            using var stream = assembly.GetManifestResourceStream(logicalName)
                ?? throw Error(SvgLoadErrorCode.EmptySource, $"SVG manifest resource '{logicalName}' was not found in '{assembly.GetName().Name}'.");
            return FromStream(stream, options);
        }

        /// <summary>Reads and validates an SVG without taking ownership of the input stream.</summary>
        public static SvgImageSource FromStream(Stream source, SvgLoadOptions options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.CanRead) throw new ArgumentException("SVG source stream must be readable.", nameof(source));
            var limits = (options ?? new SvgLoadOptions()).Snapshot();
            return Create(ReadBounded(source, limits.MaximumSourceBytes), limits);
        }

        /// <summary>Copies and validates SVG bytes from caller-owned memory.</summary>
        public static SvgImageSource FromMemory(ReadOnlyMemory<byte> source, SvgLoadOptions options = null)
        {
            var limits = (options ?? new SvgLoadOptions()).Snapshot();
            if (source.Length == 0) throw Error(SvgLoadErrorCode.EmptySource, "SVG source is empty.");
            if (source.Length > limits.MaximumSourceBytes)
                throw Error(SvgLoadErrorCode.SourceTooLarge, $"SVG source exceeds the {limits.MaximumSourceBytes}-byte limit.");
            return Create(source.ToArray(), limits);
        }

        /// <summary>Opens a read-only stream over a private copy of the validated source.</summary>
        public Stream OpenStream() => new MemoryStream(_source, writable: false);
        internal byte[] CopySource() => (byte[])_source.Clone();

        public bool Equals(SvgImageSource other) => other != null && StringComparer.Ordinal.Equals(ContentIdentity, other.ContentIdentity);
        public override bool Equals(object obj) => obj is SvgImageSource other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ContentIdentity);

        private static SvgImageSource Create(byte[] source, SvgLoadLimits limits)
        {
            if (source.Length == 0) throw Error(SvgLoadErrorCode.EmptySource, "SVG source is empty.");
            return new SvgImageSource(source, SvgPreflight.Validate(source, limits));
        }

        private static byte[] ReadBounded(Stream source, int maximumBytes)
        {
            if (source.CanSeek)
            {
                var remaining = source.Length - source.Position;
                if (remaining > maximumBytes)
                    throw Error(SvgLoadErrorCode.SourceTooLarge, $"SVG source exceeds the {maximumBytes}-byte limit.");
            }

            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                var count = source.Read(buffer, 0, Math.Min(buffer.Length, maximumBytes + 1 - (int)output.Length));
                if (count == 0) break;
                output.Write(buffer, 0, count);
                if (output.Length > maximumBytes)
                    throw Error(SvgLoadErrorCode.SourceTooLarge, $"SVG source exceeds the {maximumBytes}-byte limit.");
            }
            return output.ToArray();
        }

        private static SvgLoadException Error(SvgLoadErrorCode code, string message, Exception innerException = null) =>
            new SvgLoadException(code, message, innerException);

        private static class SvgPreflight
        {
            private static readonly HashSet<string> ForbiddenElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "animate", "animateMotion", "animateTransform", "discard", "foreignObject", "script", "set",
            };
            private static readonly HashSet<string> UnsupportedElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audio", "color-profile", "filter", "font", "font-face", "glyph", "hkern", "image",
                "text", "textPath", "tspan", "video", "vkern",
            };
            private static readonly HashSet<string> NumericAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "cx", "cy", "d", "dx", "dy", "fill-opacity", "height", "opacity", "offset", "pathLength",
                "points", "r", "rx", "ry", "stroke-dasharray", "stroke-dashoffset", "stroke-miterlimit",
                "stroke-opacity", "stroke-width", "transform", "viewBox", "width", "x", "x1", "x2", "y", "y1", "y2",
            };

            internal static SvgSourceMetadata Validate(byte[] source, SvgLoadLimits limits)
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = false,
                    MaxCharactersFromEntities = 0,
                    MaxCharactersInDocument = limits.MaximumSourceBytes,
                };

                try
                {
                    using var stream = new MemoryStream(source, writable: false);
                    using var reader = XmlReader.Create(stream, settings);
                    var metadata = new SvgSourceMetadata();
                    var rootSeen = false;
                    var attributeCount = 0;
                    var textBytes = 0;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (!rootSeen)
                            {
                                if (!reader.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                                    throw Error(SvgLoadErrorCode.InvalidRoot, "SVG source must have an svg root element.");
                                rootSeen = true;
                                ReadRootMetadata(reader, metadata, limits);
                            }
                            metadata.ElementCount++;
                            if (metadata.ElementCount > limits.MaximumElements)
                                throw Budget("element", limits.MaximumElements);
                            if (reader.Depth + 1 > limits.MaximumDepth)
                                throw Budget("nesting depth", limits.MaximumDepth);
                            if (ForbiddenElements.Contains(reader.LocalName))
                                throw Error(SvgLoadErrorCode.ForbiddenContent, $"SVG element '{reader.LocalName}' is forbidden.");
                            if (UnsupportedElements.Contains(reader.LocalName) || reader.LocalName.StartsWith("fe", StringComparison.OrdinalIgnoreCase))
                                throw Error(SvgLoadErrorCode.UnsupportedFeature, $"SVG element '{reader.LocalName}' is not supported.");
                            attributeCount += reader.AttributeCount;
                            if (attributeCount > limits.MaximumAttributes)
                                throw Budget("attribute", limits.MaximumAttributes);
                            ValidateAttributes(reader, metadata, limits);
                        }
                        else if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
                        {
                            textBytes = checked(textBytes + Encoding.UTF8.GetByteCount(reader.Value));
                            if (textBytes > limits.MaximumTextBytes)
                                throw Budget("text byte", limits.MaximumTextBytes);
                            ValidateStyleText(reader.Value, metadata, limits);
                        }
                        else if (reader.NodeType == XmlNodeType.DocumentType)
                        {
                            throw Error(SvgLoadErrorCode.ForbiddenContent, "SVG document types and entities are forbidden.");
                        }
                        else if (reader.NodeType == XmlNodeType.ProcessingInstruction)
                        {
                            throw Error(SvgLoadErrorCode.ForbiddenContent, "SVG processing instructions are forbidden.");
                        }
                    }
                    if (!rootSeen) throw Error(SvgLoadErrorCode.InvalidRoot, "SVG source must have an svg root element.");
                    metadata.ValidateReferenceGraph();
                    metadata.ResolveIntrinsicSize(limits);
                    return metadata;
                }
                catch (SvgLoadException)
                {
                    throw;
                }
                catch (XmlException exception)
                {
                    throw Error(SvgLoadErrorCode.InvalidXml, "SVG source is not well-formed XML.", exception);
                }
                catch (OverflowException exception)
                {
                    throw Error(SvgLoadErrorCode.BudgetExceeded, "SVG source exceeded a numeric validation budget.", exception);
                }
            }

            private static void ReadRootMetadata(XmlReader reader, SvgSourceMetadata metadata, SvgLoadLimits limits)
            {
                metadata.Width = ParseOptionalLength(reader.GetAttribute("width"), "width");
                metadata.Height = ParseOptionalLength(reader.GetAttribute("height"), "height");
                metadata.ViewBox = ParseViewBox(reader.GetAttribute("viewBox"));
                metadata.PreserveAspectRatio = reader.GetAttribute("preserveAspectRatio") ?? "xMidYMid meet";
                if (metadata.Width.HasValue) ValidateDimension(metadata.Width.Value, "width", limits);
                if (metadata.Height.HasValue) ValidateDimension(metadata.Height.Value, "height", limits);
            }

            private static void ValidateAttributes(XmlReader reader, SvgSourceMetadata metadata, SvgLoadLimits limits)
            {
                if (!reader.HasAttributes) return;
                var elementId = reader.GetAttribute("id");
                if (!string.IsNullOrEmpty(elementId)) metadata.AddElementId(elementId);
                while (reader.MoveToNextAttribute())
                {
                    if (reader.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                        throw Error(SvgLoadErrorCode.ForbiddenContent, $"SVG event attribute '{reader.LocalName}' is forbidden.");
                    if (reader.LocalName.Equals("color-profile", StringComparison.OrdinalIgnoreCase))
                        throw Error(SvgLoadErrorCode.UnsupportedFeature, "SVG color profiles are not supported.");
                    if (reader.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase))
                        ValidateReference(reader.Value, elementId, metadata, limits);
                    if (NumericAttributes.Contains(reader.LocalName) && ContainsNonFiniteNumber(reader.Value))
                        throw Error(SvgLoadErrorCode.InvalidDimensions, $"SVG numeric attribute '{reader.LocalName}' contains a non-finite value.");
                    ValidateUnsupportedCss(reader.Value);
                    ValidateCssUrls(reader.Value, elementId, metadata, limits);
                }
                reader.MoveToElement();
            }

            private static void ValidateReference(string value, string elementId, SvgSourceMetadata metadata, SvgLoadLimits limits)
            {
                value = value?.Trim();
                if (string.IsNullOrEmpty(value)) return;
                if (!value.StartsWith("#", StringComparison.Ordinal))
                    throw Error(SvgLoadErrorCode.ExternalReference, "SVG external file, network, and data references are forbidden.");
                if (value.Length == 1) throw Error(SvgLoadErrorCode.InvalidXml, "SVG local reference requires an identifier.");
                metadata.LocalReferenceCount++;
                if (metadata.LocalReferenceCount > limits.MaximumLocalReferences)
                    throw Budget("local reference", limits.MaximumLocalReferences);
                metadata.AddReference(elementId, value.Substring(1));
            }

            private static void ValidateCssUrls(string value, string elementId, SvgSourceMetadata metadata, SvgLoadLimits limits)
            {
                if (string.IsNullOrEmpty(value)) return;
                var searchIndex = 0;
                while ((searchIndex = value.IndexOf("url(", searchIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var close = value.IndexOf(')', searchIndex + 4);
                    if (close < 0) throw Error(SvgLoadErrorCode.InvalidXml, "SVG CSS url reference is incomplete.");
                    var reference = value.Substring(searchIndex + 4, close - searchIndex - 4).Trim().Trim('\'', '"');
                    ValidateReference(reference, elementId, metadata, limits);
                    searchIndex = close + 1;
                }
            }

            private static void ValidateStyleText(string value, SvgSourceMetadata metadata, SvgLoadLimits limits)
            {
                if (string.IsNullOrEmpty(value)) return;
                if (value.IndexOf("@import", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("@font-face", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw Error(SvgLoadErrorCode.ExternalReference, "SVG external stylesheets and fonts are forbidden.");
                ValidateUnsupportedCss(value);
                ValidateCssUrls(value, null, metadata, limits);
            }

            private static void ValidateUnsupportedCss(string value)
            {
                if (value.IndexOf("mix-blend-mode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("background-blend-mode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("color-profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("icc-color", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw Error(SvgLoadErrorCode.UnsupportedFeature, "SVG blend modes and color profiles are not supported.");
            }

            private static bool ContainsNonFiniteNumber(string value) =>
                !string.IsNullOrEmpty(value) &&
                (ContainsToken(value, "nan") || ContainsToken(value, "infinity"));

            private static bool ContainsToken(string value, string token)
            {
                var index = 0;
                while ((index = value.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var before = index == 0 || !char.IsLetter(value[index - 1]);
                    var afterIndex = index + token.Length;
                    var after = afterIndex == value.Length || !char.IsLetter(value[afterIndex]);
                    if (before && after) return true;
                    index = afterIndex;
                }
                return false;
            }

            private static float? ParseOptionalLength(string value, string name)
            {
                if (string.IsNullOrWhiteSpace(value)) return null;
                value = value.Trim();
                if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) value = value.Substring(0, value.Length - 2).TrimEnd();
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !float.IsFinite(result) || result <= 0)
                    throw Error(SvgLoadErrorCode.InvalidDimensions, $"SVG {name} must be a finite positive number or px length.");
                return result;
            }

            private static RectangleF? ParseViewBox(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return null;
                var parts = value.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4) throw Error(SvgLoadErrorCode.InvalidDimensions, "SVG viewBox must contain four finite numbers.");
                var values = new float[4];
                for (var index = 0; index < values.Length; index++)
                    if (!float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]) || !float.IsFinite(values[index]))
                        throw Error(SvgLoadErrorCode.InvalidDimensions, "SVG viewBox must contain four finite numbers.");
                if (values[2] <= 0 || values[3] <= 0)
                    throw Error(SvgLoadErrorCode.InvalidDimensions, "SVG viewBox width and height must be positive.");
                return new RectangleF(values[0], values[1], values[2], values[3]);
            }

            internal static void ValidateDimension(float value, string name, SvgLoadLimits limits)
            {
                if (value > limits.MaximumDimension)
                    throw Error(SvgLoadErrorCode.BudgetExceeded, $"SVG {name} exceeds the {limits.MaximumDimension}-unit limit.");
            }

            private static SvgLoadException Budget(string budget, long limit) =>
                Error(SvgLoadErrorCode.BudgetExceeded, $"SVG source exceeds the {limit} {budget} limit.");
        }

        private sealed class SvgSourceMetadata
        {
            private readonly HashSet<string> _elementIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<string>> _references = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            internal float? Width { get; set; }
            internal float? Height { get; set; }
            internal RectangleF? ViewBox { get; set; }
            internal string PreserveAspectRatio { get; set; }
            internal int ElementCount { get; set; }
            internal int LocalReferenceCount { get; set; }
            internal Vector2 IntrinsicSize { get; private set; }

            internal void AddElementId(string elementId)
            {
                if (!_elementIds.Add(elementId))
                    throw Error(SvgLoadErrorCode.InvalidXml, $"SVG element id '{elementId}' is duplicated.");
            }

            internal void AddReference(string elementId, string targetId)
            {
                if (string.IsNullOrEmpty(elementId)) return;
                if (!_references.TryGetValue(elementId, out var targets))
                {
                    targets = new List<string>();
                    _references[elementId] = targets;
                }
                targets.Add(targetId);
            }

            internal void ValidateReferenceGraph()
            {
                var visiting = new HashSet<string>(StringComparer.Ordinal);
                var visited = new HashSet<string>(StringComparer.Ordinal);
                foreach (var elementId in _references.Keys) Visit(elementId, visiting, visited);
            }

            private void Visit(string elementId, HashSet<string> visiting, HashSet<string> visited)
            {
                if (visited.Contains(elementId)) return;
                if (!visiting.Add(elementId))
                    throw Error(SvgLoadErrorCode.ForbiddenContent, "SVG local references contain a cycle.");
                if (_references.TryGetValue(elementId, out var targets))
                    foreach (var target in targets)
                        if (_elementIds.Contains(target)) Visit(target, visiting, visited);
                visiting.Remove(elementId);
                visited.Add(elementId);
            }

            internal void ResolveIntrinsicSize(SvgLoadLimits limits)
            {
                var width = Width ?? ViewBox?.Width;
                var height = Height ?? ViewBox?.Height;
                if (!width.HasValue || !height.HasValue)
                    throw Error(SvgLoadErrorCode.InvalidDimensions, "SVG source requires positive width and height or a viewBox.");
                SvgPreflight.ValidateDimension(width.Value, "width", limits);
                SvgPreflight.ValidateDimension(height.Value, "height", limits);
                if ((double)width.Value * height.Value > limits.MaximumPixelArea)
                    throw Error(SvgLoadErrorCode.BudgetExceeded, $"SVG intrinsic area exceeds the {limits.MaximumPixelArea}-pixel limit.");
                IntrinsicSize = new Vector2(width.Value, height.Value);
            }
        }
    }
}