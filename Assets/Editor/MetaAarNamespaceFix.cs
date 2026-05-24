using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

public sealed class MetaAarNamespaceFix : IPostGenerateGradleAndroidProject
{
    private const string OculusIntegrationPackage = "package=\"com.oculus.Integration\"";

    private static readonly Dictionary<string, string> AarPackages = new()
    {
        { "InteractionSdk.aar", "com.oculus.integration.interactionsdk" },
        { "SDKTelemetry.aar", "com.oculus.integration.sdktelemetry" },
        { "OVRPlugin.aar", "com.oculus.integration.ovrplugin" },
    };

    public int callbackOrder => int.MaxValue;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var libsDirectory = ResolveUnityLibraryPath(path);
        if (string.IsNullOrEmpty(libsDirectory))
        {
            Debug.LogWarning($"Meta AAR namespace fix skipped: unable to find unityLibrary/libs from '{path}'.");
            return;
        }

        foreach (var aarPackage in AarPackages)
        {
            var aarPath = Path.Combine(libsDirectory, aarPackage.Key);
            if (!File.Exists(aarPath))
            {
                continue;
            }

            PatchManifestPackage(aarPath, aarPackage.Value);
        }
    }

    private static string ResolveUnityLibraryPath(string generatedProjectPath)
    {
        var directPath = Path.Combine(generatedProjectPath, "libs");
        if (Directory.Exists(directPath))
        {
            return directPath;
        }

        var nestedPath = Path.Combine(generatedProjectPath, "unityLibrary", "libs");
        return Directory.Exists(nestedPath) ? nestedPath : null;
    }

    private static void PatchManifestPackage(string aarPath, string packageName)
    {
        const string manifestName = "AndroidManifest.xml";
        var tempPath = aarPath + ".tmp";
        var changed = false;

        using (var sourceStream = File.OpenRead(aarPath))
        using (var sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
        using (var targetStream = File.Create(tempPath))
        using (var targetArchive = new ZipArchive(targetStream, ZipArchiveMode.Create))
        {
            foreach (var sourceEntry in sourceArchive.Entries)
            {
                var targetEntry = targetArchive.CreateEntry(sourceEntry.FullName, System.IO.Compression.CompressionLevel.Optimal);
                targetEntry.LastWriteTime = sourceEntry.LastWriteTime;

                using var sourceEntryStream = sourceEntry.Open();
                using var targetEntryStream = targetEntry.Open();

                if (string.Equals(sourceEntry.FullName, manifestName, StringComparison.Ordinal))
                {
                    using var reader = new StreamReader(sourceEntryStream, Encoding.UTF8);
                    var manifest = reader.ReadToEnd();
                    var patchedManifest = manifest.Replace(OculusIntegrationPackage, $"package=\"{packageName}\"");
                    changed = !string.Equals(manifest, patchedManifest, StringComparison.Ordinal);

                    using var writer = new StreamWriter(targetEntryStream, new UTF8Encoding(false));
                    writer.Write(patchedManifest);
                }
                else
                {
                    sourceEntryStream.CopyTo(targetEntryStream);
                }
            }
        }

        if (changed)
        {
            File.Copy(tempPath, aarPath, true);
            Debug.Log($"Patched Meta AAR manifest namespace: {Path.GetFileName(aarPath)} -> {packageName}");
        }

        File.Delete(tempPath);
    }
}
