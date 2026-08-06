// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml.Build;
using Forma.Xaml.Compiler;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Forma.Xaml.Compiler.Tests;

public class SvgAssetBuildTaskTest
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"forma-svg-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void ValidAssetProducesDeterministicLogicalNameAndMetadata()
    {
        var file = Write("Assets/icon.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='18' height='12'><rect width='18' height='12' /></svg>");
        var first = Execute(file);
        var second = Execute(file);

        Assert.Multiple(() =>
        {
            Assert.That(first.Success, Is.True);
            Assert.That(first.Task.ValidatedSvgFiles, Has.Length.EqualTo(1));
            Assert.That(first.Task.ValidatedSvgFiles[0].GetMetadata("LogicalName"), Is.EqualTo(SvgAssetLogicalName.Create("Fixture", _directory, file)));
            Assert.That(second.Task.ValidatedSvgFiles[0].GetMetadata("LogicalName"), Is.EqualTo(first.Task.ValidatedSvgFiles[0].GetMetadata("LogicalName")));
        });
    }

    [TestCase("missing.svg", FormaDiagnosticCodes.SvgAssetMissing)]
    [TestCase("invalid.svg", FormaDiagnosticCodes.SvgAssetInvalid)]
    [TestCase("outside.svg", FormaDiagnosticCodes.SvgAssetInvalid)]
    public void InvalidAssetsReportDeterministicDiagnostic(string fixture, string expectedCode)
    {
        var file = fixture switch
        {
            "invalid.svg" => Write(fixture, "<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.com/a.png' /></svg>"),
            "outside.svg" => WriteOutsideProject("<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1' />"),
            _ => Path.Combine(_directory, fixture),
        };

        var result = Execute(file);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Engine.Errors.Select(error => error.Code), Does.Contain(expectedCode));
            Assert.That(result.Task.ValidatedSvgFiles, Is.Empty);
        });
    }

    [Test]
    public void DuplicateAssetReportsDeterministicDiagnostic()
    {
        var file = Write("Assets/icon.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1' />");
        var result = Execute(file, file);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Engine.Errors.Select(error => error.Code), Does.Contain(FormaDiagnosticCodes.SvgAssetDuplicate));
            Assert.That(result.Task.ValidatedSvgFiles, Has.Length.EqualTo(1));
        });
    }

    private (bool Success, ValidateFormaSvg Task, RecordingBuildEngine Engine) Execute(params string[] files)
    {
        var engine = new RecordingBuildEngine();
        var task = new ValidateFormaSvg
        {
            AssemblyName = "Fixture",
            ProjectDirectory = _directory,
            SvgFiles = files.Select(file => (ITaskItem)new TaskItem(file)).ToArray(),
            BuildEngine = engine,
        };
        return (task.Execute(), task, engine);
    }

    private string Write(string relativePath, string source)
    {
        var file = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, source);
        return file;
    }

    private string WriteOutsideProject(string source)
    {
        var file = Path.Combine(Path.GetTempPath(), $"forma-svg-outside-{Guid.NewGuid():N}.svg");
        File.WriteAllText(file, source);
        return file;
    }

    private sealed class RecordingBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "Fixture.csproj";
        public void LogErrorEvent(BuildErrorEventArgs args) => Errors.Add(args);
        public void LogWarningEvent(BuildWarningEventArgs args) { }
        public void LogMessageEvent(BuildMessageEventArgs args) { }
        public void LogCustomEvent(CustomBuildEventArgs args) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => false;
    }
}