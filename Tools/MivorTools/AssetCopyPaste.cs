// Credits: https://github.com/Jashepp & https://dev.mivor.net/VRChat/
// Version: 0.0.1

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using UnityEditor;

namespace MivorTools.Editor
{
	public class AssetCopyPaste
	{
		// https://docs.unity3d.com/2019.4/Documentation/ScriptReference/MenuItem.html
		private const string copyHotkey = "&c";
		private const string pasteHotkey = "&v";

		protected static List<(UnityObject,string)> clipboard = new List<(UnityObject,string)>();

		// Copy
		[MenuItem("Assets/[CopyPaste] Copy "+copyHotkey, false, 1200)]
		public static void menuAssetCopy(){
			if(Selection.objects.Length==0) return;
			foreach(UnityObject obj in Selection.objects){
				var path = AssetDatabase.GetAssetPath(obj);
				bool inClipboard = false;
				foreach((UnityObject obj2, string path2) in clipboard){
					if(path2==path){ inClipboard=true; break; }
				}
				if(inClipboard) continue;
				clipboard.Add((obj,path));
			}
		}
		[MenuItem("Assets/[CopyPaste] Copy "+copyHotkey, true, 1200)]
		public static bool menuAssetCopy_validate(){
			if(Selection.objects.Length<=0) return false;
			return true;
		}
		
		// Paste
		[MenuItem("Assets/[CopyPaste] Paste "+pasteHotkey, false, 1200)]
		public static void menuAssetPaste(){
			if(Selection.objects.Length!=1) return;
			//AssetDatabase.Refresh();
			if(Selection.objects[0] is UnityEditor.DefaultAsset folder){
				var dirPath = AssetDatabase.GetAssetPath(folder);
				if(dirPath.Length>0 && !AssetDatabase.IsValidFolder(dirPath)) dirPath = dirPath.Substring(0,dirPath.LastIndexOf("/"));
				if(dirPath.Length<=0) return;
				bool fileExists(string path){
					UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(path);
					return objs.Length>0;
				}
				foreach((UnityObject obj, string path) in clipboard){
					string baseName = path.Substring(path.LastIndexOf("/")+1);
					string newPath = dirPath+"/"+baseName;
					if(path==newPath) continue;
					if(fileExists(newPath)){
						bool btn1 = EditorUtility.DisplayDialog("Copy-Paste Confirmation","\""+baseName+"\" exists at destination.","Rename new item","Rename old item");
						if(btn1){
							bool renamed = false;
							for(int i=2;i<1000;i++){
								string newPath2; string extra = " - Copy ("+i+")";
								if(!baseName.Contains(".")) newPath2 = dirPath+"/"+baseName+extra;
								else newPath2 = dirPath+"/"+baseName.Substring(0,baseName.LastIndexOf("."))+extra+baseName.Substring(baseName.LastIndexOf("."));
								if(!fileExists(newPath2)){
									newPath = newPath2;
									renamed = true;
									break;
								}
							}
							if(!renamed){
								Debug.LogWarning("Failed to find new filename for new file: "+newPath);
								continue;
							}
						}
						else {
							bool renamed = false;
							for(int i=2;i<1000;i++){
								string oldPath; string extra = " - Old ("+i+")";
								if(!baseName.Contains(".")) oldPath = newPath+extra;
								else oldPath = dirPath+"/"+baseName.Substring(0,baseName.LastIndexOf("."))+extra+baseName.Substring(baseName.LastIndexOf("."));
								if(!fileExists(oldPath)){
									string err = AssetDatabase.MoveAsset(newPath,oldPath);
									if(err!=null && err!=""){
										Debug.LogWarning("Error moving file: "+err);
										break;
									}
									renamed = true;
									break;
								}
							}
							if(!renamed){
								Debug.LogWarning("Failed to find new filename for existing file: "+newPath);
								continue;
							}
						}
					}
					bool result = AssetDatabase.CopyAsset(path,newPath);
					if(!result) Debug.LogWarning("Failed to copy-paste files: "+path+", to: "+newPath);
				}
				clipboard.Clear();
			}
		}
		[MenuItem("Assets/[CopyPaste] Paste "+pasteHotkey, true, 1200)]
		public static bool menuAssetPaste_validate(){
			if(Selection.objects.Length!=1 || clipboard.Count==0) return false;
			if(!(Selection.objects[0] is UnityEditor.DefaultAsset)) return false;
			var path = AssetDatabase.GetAssetPath(Selection.objects[0]);
			if(!AssetDatabase.IsValidFolder(path)) return false;
			return true;
		}
		
		// Clear
		[MenuItem("Assets/[CopyPaste] Clear", false, 1200)]
		public static void menuAssetClear(){
			clipboard.Clear();
		}
		[MenuItem("Assets/[CopyPaste] Clear", true, 1200)]
		public static bool menuAssetClear_validate(){
			return clipboard.Count>0;
		}
		
	}
}

#endif
