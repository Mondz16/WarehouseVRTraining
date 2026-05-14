using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Valari.TrainingTemplate.Editor
{
    internal static class ValariPackageExporter
    {
        private const string PackageFolderRelative = "Packages/com.valari.foundrytraining.training";

        [MenuItem("Tools/Valari/Export Package (zip)...", priority = 10)]
        public static void ExportZipMenu()
        {
            ExportZipInteractive();
        }

        [MenuItem("Tools/Valari/Export Package (.unitypackage)...", priority = 11)]
        public static void ExportUnityPackageMenu()
        {
            ExportUnityPackageInteractive();
        }

        internal static void ExportZipInteractive()
        {
            var packageFolderAbsolute = Path.GetFullPath(PackageFolderRelative);
            if (!Directory.Exists(packageFolderAbsolute))
            {
                EditorUtility.DisplayDialog(
                    "Export Package",
                    $"Package folder not found:\n{packageFolderAbsolute}",
                    "OK");
                return;
            }

            var defaultName = "com.valari.training.template.zip";
            var zipPath = EditorUtility.SaveFilePanel(
                "Export UPM Package (zip)",
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                defaultName,
                "zip");

            if (string.IsNullOrWhiteSpace(zipPath))
                return;

            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                AddFolderToZip(zip, packageFolderAbsolute, packageFolderAbsolute);

                EditorUtility.RevealInFinder(zipPath);
                Debug.Log($"Exported package zip to: {zipPath}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog(
                    "Export Package",
                    $"Failed to export zip.\n\n{ex.GetType().Name}: {ex.Message}",
                    "OK");
            }
        }

        internal static void ExportUnityPackageInteractive()
        {
            var packageFolderAbsolute = Path.GetFullPath(PackageFolderRelative);
            if (!Directory.Exists(packageFolderAbsolute))
            {
                EditorUtility.DisplayDialog(
                    "Export Package",
                    $"Package folder not found:\n{packageFolderAbsolute}",
                    "OK");
                return;
            }

            var defaultName = "com.valari.training.template.unitypackage";
            var unityPackagePath = EditorUtility.SaveFilePanel(
                "Export Package (.unitypackage)",
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                defaultName,
                "unitypackage");

            if (string.IsNullOrWhiteSpace(unityPackagePath))
                return;

            try
            {
                // Unity's ExportPackage works with Assets/ paths, not Packages/.
                // Stage the embedded package to a temporary Assets/ folder, export it, then clean up.
                const string stagingRoot = "Assets/__ValariPackageExportStaging";
                const string stagingFolder = stagingRoot + "/com.valari.training.template";

                if (AssetDatabase.IsValidFolder(stagingRoot))
                    FileUtil.DeleteFileOrDirectory(stagingRoot);

                Directory.CreateDirectory(stagingFolder);

                // Copy package contents into staging folder.
                CopyDirectory(packageFolderAbsolute, Path.GetFullPath(stagingFolder));

                AssetDatabase.Refresh();

                AssetDatabase.ExportPackage(
                    new[] { stagingRoot },
                    unityPackagePath,
                    ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

                FileUtil.DeleteFileOrDirectory(stagingRoot);
                AssetDatabase.Refresh();

                EditorUtility.RevealInFinder(unityPackagePath);
                Debug.Log($"Exported .unitypackage to: {unityPackagePath}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog(
                    "Export Package",
                    $"Failed to export .unitypackage.\n\n{ex.GetType().Name}: {ex.Message}",
                    "OK");
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, directory);
                var dest = Path.Combine(destinationDir, relative);
                Directory.CreateDirectory(dest);
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file);
                var dest = Path.Combine(destinationDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? destinationDir);
                File.Copy(file, dest, true);
            }
        }

        private static void AddFolderToZip(ZipArchive zip, string folderAbsolute, string rootAbsolute)
        {
            foreach (var file in Directory.GetFiles(folderAbsolute))
            {
                var relative = Path.GetRelativePath(rootAbsolute, file).Replace('\\', '/');
                zip.CreateEntryFromFile(file, relative, System.IO.Compression.CompressionLevel.Optimal);
            }

            foreach (var dir in Directory.GetDirectories(folderAbsolute))
            {
                var name = Path.GetFileName(dir);
                if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
                    continue;

                AddFolderToZip(zip, dir, rootAbsolute);
            }
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class ValariExportPackageButton : EditorToolbarButton
    {
        public const string Id = "Valari/ExportTrainingTemplatePackage";

        public ValariExportPackageButton()
        {
            text = "Export Package";
            tooltip = "Export com.valari.training.template (.unitypackage)";
            clicked += ValariPackageExporter.ExportUnityPackageInteractive;
        }
    }
}

