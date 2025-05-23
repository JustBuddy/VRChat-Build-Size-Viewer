/**
 * VRC Build Size Viewer
 * Created by MunifiSense
 * https://github.com/MunifiSense/VRChat-Build-Size-Viewer
 */

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class BuildSizeViewer : EditorWindow {
  public class BuildObject {
    public string size;
    public string percent;
    public string path;
    public long sizeInBytes; // Add this property
    public Type assetType; // Add this property to store the asset type
  }

  private List<BuildObject> buildObjectList;
  private List<string> uncompressedList;
  private string buildLogPath =
      System.Environment.GetFolderPath(
          System.Environment.SpecialFolder.LocalApplicationData) +
      "/Unity/Editor/Editor.log";
  private char[] delimiterChars = {' ', '\t'};
  private float win;
  private float w1;
  private float w2;
  private float w3;
  private string totalSize;
  private bool buildLogFound = false;
  private Vector2 scrollPos;
  private string currentFilter = "All"; // Default filter

  [MenuItem("Window/Muni/VRC Build Size Viewer")]
  public static void ShowWindow() {
    EditorWindow.GetWindow(typeof(BuildSizeViewer));
  }

  private void OnGUI() {
    win = (float)(position.width * 0.6);
    w1 = (float)(win * 0.15);
    w2 = (float)(win * 0.15);
    w3 = (float)(win * 0.35);

    EditorGUILayout.LabelField("VRC Build Size Viewer", EditorStyles.boldLabel);
    EditorGUILayout.LabelField(
        "Create a build of your world/avatar and click the button!",
        EditorStyles.label);

    if (GUILayout.Button("Read Build Log")) {
      buildLogFound = false;
      buildLogFound = GetBuildSize();
    }

    if (buildLogFound) {
      // Filter Buttons
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("All")) {
        currentFilter = "All";
      }
      if (GUILayout.Button("Texture")) {
        currentFilter = "Texture";
      }
      if (GUILayout.Button("AudioClip")) {
        currentFilter = "AudioClip";
      }
      if (GUILayout.Button("Mesh")) {
        currentFilter = "Mesh";
      }
      if (GUILayout.Button("Material")) {
        currentFilter = "Material";
      }

      EditorGUILayout.EndHorizontal();

      if (uncompressedList != null && uncompressedList.Count != 0) {
        EditorGUILayout.LabelField(
            "Total Compressed Build Size: " + totalSize);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.Separator();
        EditorGUILayout.EndHorizontal();
        foreach (string s in uncompressedList) {
          EditorGUILayout.LabelField(s);
        }
      }

      if (buildObjectList != null && buildObjectList.Count != 0) {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.Separator();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Size%", GUILayout.Width(w1));
        EditorGUILayout.LabelField("Size", GUILayout.Width(w2));
        EditorGUILayout.LabelField("Path", GUILayout.Width(w3));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.Separator();
        EditorGUILayout.EndHorizontal();

        foreach (BuildObject buildObject in buildObjectList) {
          // Apply Filter
          bool shouldShow = false;
          switch (currentFilter) {
            case "All":
              shouldShow = true;
              break;
            case "Texture":
              shouldShow = buildObject.assetType == typeof(Texture2D);
              break;
            case "AudioClip":
              shouldShow = buildObject.assetType == typeof(AudioClip);
              break;
            case "Mesh":
              shouldShow = buildObject.assetType == typeof(Mesh);
              break;
            case "Material":
              shouldShow = buildObject.assetType == typeof(Material);
              break;
          }

          if (shouldShow) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(buildObject.percent, GUILayout.Width(w1));
            EditorGUILayout.LabelField(buildObject.size, GUILayout.Width(w2));
            EditorGUILayout.LabelField(buildObject.path);
            if (buildObject.path != "Resources/unity_builtin_extra") {
              if (GUILayout.Button("Go", GUILayout.Width(w1))) {
                Object obj = AssetDatabase.LoadAssetAtPath(
                    buildObject.path, typeof(Object));
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
              }
            }
            EditorGUILayout.EndHorizontal();
          }
        }
        EditorGUILayout.EndScrollView();
      }
    }
  }

  private bool GetBuildSize() {
    // Read the text from log
    FileUtil.ReplaceFile(buildLogPath, buildLogPath + "copy");
    StreamReader reader = new StreamReader(buildLogPath + "copy");

    if (reader == null) {
      Debug.LogWarning("Could not read build file.");
      FileUtil.DeleteFileOrDirectory(buildLogPath + "copy");
      return false;
    }

    string line = reader.ReadLine();
    while (line != null) {
      if (
          (line.Contains("scene-") && line.Contains(".vrcw")) ||
          (line.Contains("avtr") && line.Contains(".prefab.unity3d"))
      ) {
        buildObjectList = new List<BuildObject>();
        uncompressedList = new List<string>();
        line = reader.ReadLine();
        while (!line.Contains("Compressed Size")) {
          line = reader.ReadLine();
        }
        totalSize = line.Split(':')[1];
        line = reader.ReadLine();
        while (
            line !=
            "Used Assets and files from the Resources folder, sorted by " +
                "uncompressed size:") {
          uncompressedList.Add(line);
          line = reader.ReadLine();
        }
        line = reader.ReadLine();
        while (
            line !=
            "-----------------------------------------------------------------" +
                "--------------") {
          string[] splitLine = line.Split(delimiterChars);
          BuildObject temp = new BuildObject();
          temp.size = splitLine[1] + splitLine[2];
          temp.percent = splitLine[4];
          temp.path = splitLine[5];
          for (int i = 6; i < splitLine.Length; i++) {
            temp.path += (" " + splitLine[i]);
          }

          // Determine asset type
          temp.assetType = AssetDatabase.GetMainAssetTypeAtPath(temp.path);
          buildObjectList.Add(temp);
          line = reader.ReadLine();
        }

        // Aggregate package data
        AggregatePackageSizes();
      }
      line = reader.ReadLine();
    }
    FileUtil.DeleteFileOrDirectory(buildLogPath + "copy");
    reader.Close();
    return true;
  }

  private void AggregatePackageSizes() {
    string packagesPath = Path.Combine(Application.dataPath, "../Packages");
    DirectoryInfo packageDir = new DirectoryInfo(packagesPath);

    if (!packageDir.Exists) {
      Debug.LogWarning("Packages directory not found: " + packagesPath);
      return;
    }

    // Filter out the assets that are from packages
    List<BuildObject> packageAssets = buildObjectList.Where(
        asset => asset.path.StartsWith("Packages/")).ToList();

    if (packageAssets.Count == 0) {
      Debug.Log("No package assets found in build log.");
      return;
    }

    // Group assets by package name
    var packageGroups = packageAssets.GroupBy(asset => {
      // Extract the package name from the path
      string path = asset.path;
      string[] parts = path.Split('/');
      if (parts.Length > 1) {
        return parts[1]; // The second part of the path is the package name
      }
      return "Unknown Package";
    });

    // Create a combined package entry for each package
    List<BuildObject> packageEntries = new List<BuildObject>();
    foreach (var packageGroup in packageGroups) {
      string packageName = packageGroup.Key;
      float totalPackageSizeInBytes = 0;

      foreach (BuildObject packageAsset in packageGroup) {
        // Extract size value, handling 'kb', 'mb', 'bytes'
        string sizeString = packageAsset.size.ToLower();
        float sizeValue = float.Parse(
            sizeString.Replace("kb", "").Replace("mb", "").Replace("bytes", ""));

        if (sizeString.Contains("kb")) {
          sizeValue *= 1024;
        } else if (sizeString.Contains("mb")) {
          sizeValue *= 1024 * 1024;
        }

        totalPackageSizeInBytes += sizeValue;
      }

      // Convert to human-readable size
      string packageSizeString =
          EditorUtility.FormatBytes((long)totalPackageSizeInBytes);

      // Create a combined package entry
      BuildObject packageEntry = new BuildObject {
        size = packageSizeString,
        percent =
            "Package (Combined)",  // Not adding to overall size, so no percentage
        path = packageName,
        sizeInBytes = (long)totalPackageSizeInBytes,  // Store size in bytes
        assetType = null  // Packages aren't a specific asset type.
      };
      packageEntries.Add(packageEntry);
    }

    // Sort the package entries by size (descending)
    packageEntries = packageEntries.OrderByDescending(p => p.sizeInBytes).ToList();

    // Insert the combined package entries at the beginning
    buildObjectList.InsertRange(0, packageEntries);
  }
}
#endif
