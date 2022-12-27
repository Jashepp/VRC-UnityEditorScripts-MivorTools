// Credits: https://github.com/Jashepp & https://dev.mivor.net/VRChat/
// Version: 0.0.1

#if UNITY_2019_4_OR_NEWER
#if UNITY_EDITOR
#if VRC_SDK_VRCSDK3
using System;
using SystemObject = System.Object;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using UnityEditor;
using IEnumerator = System.Collections.IEnumerator;

namespace MivorTools.Editor
{
	public partial class CheckScripts : BaseWindow<CheckScripts>
	{
		protected new const int toolVersion = 1;
		protected new const string editorScriptName = "Check Editor Scripts";
		protected new const string editorWindowName = "Check Scripts";
		protected new const bool debugLog = true;

		// Main Menu item
		[MenuItem("Tools/"+toolName+"/"+editorScriptName, false)]
		public new static void Init(){
			BaseWindow<CheckScripts>.Init();
			window.titleContent = new GUIContent(editorWindowName);
		}
		
		// Function called on GUI creation
		public void OnGUI(){
			if(defaultGUIStyle==null) defaultGUIStyle = new GUIStyle() { richText=true, alignment=TextAnchor.MiddleLeft };
			if(script_GUI_Top()){
				script_GUI_Main();
			}
		}

		// Script Vars & Functions
		private void scriptLog(string text){
			if(debugLog){ Debug.Log("Script: "+editorScriptName+" - "+text); }
		}
		private bool script_GUI_Top(){
			EditorGUILayout.BeginHorizontal();
			GUIAddLabel("Script: "+editorScriptName+" v"+toolVersion+" ","",TextAnchor.MiddleLeft,"AutoMinWidth");
			GUIAddLabel("<color='#F0F0F0'><b>["+toolName+"]</b></color> ","More at: "+toolLink,TextAnchor.MiddleRight,"AutoMinWidth");
			if(Event.current.type==EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) Application.OpenURL(toolLinkFull);
			EditorGUILayout.EndHorizontal();
			if(EditorApplication.isPlaying){
				EditorGUILayout.Space(10);
				GUIAddLabel("Please exit <color='#F0F0F0'><b>Play Mode</b></color> to use this script.","",TextAnchor.MiddleCenter);
				checksRan = false;
				return false;
			}
			return true;
		}

		private bool toggleSettings = false;
		private bool toggleShowAllResults = false;
		private bool toggleAllowEditing = false;
		private Vector2 _mainScrollingPosition;

		private bool checksRan = false;
		private bool checksRanOnce = false;
		private bool checksRunning = false;

		private void script_GUI_Main(){
			bool queueRunCheck = false;

			//EditorGUILayout.BeginVertical(GUI.skin.box);
			Rect guiRect = EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("HelpBox")){ richText=true, alignment=TextAnchor.MiddleLeft, fontSize=defaultGUIStyle.fontSize });
			toggleSettings = EditorGUILayout.Toggle(toggleSettings, EditorStyles.foldout, GUILayout.MaxWidth(15.0f));
			GUIAddLabel("Toggle Settings");
			toggleSettings = GUI.Toggle(guiRect, toggleSettings, GUIContent.none, new GUIStyle());
			EditorGUILayout.EndHorizontal();
			if(toggleSettings){
				EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.box){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(10,0,0,0) });
				toggleShowAllResults = EditorGUILayout.Toggle("Show All Results",toggleShowAllResults);
				toggleAllowEditing = EditorGUILayout.Toggle("Allow Editing",toggleAllowEditing);
				EditorGUILayout.EndVertical();
			}
			//EditorGUILayout.EndVertical();

			//EditorGUILayout.HelpBox("Both object and folder must be selected to continue.",MessageType.None);
			//EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("HelpBox")){ richText=true, alignment=TextAnchor.MiddleCenter, fontSize=defaultGUIStyle.fontSize });
			//GUIAddLabel("Some Message.","",TextAnchor.MiddleCenter);
			//EditorGUILayout.EndVertical();

			if(checksRunning){
				GUIAddLabel("<color='#FFFFFF'>Checking References, Please Wait...</color>","",new GUIStyle(GUI.skin.GetStyle("HelpBox")){ richText=true, alignment=TextAnchor.MiddleCenter, fontSize=defaultGUIStyle.fontSize });
			}
			else {
				EditorGUILayout.BeginHorizontal();
				var btn_runChecks = GUILayout.Button(new GUIContent(checksRanOnce?"Run Checks Again":"Run Checks",""));
				if(queueRunCheck){ btn_runChecks=true; queueRunCheck=false; }
				EditorGUILayout.EndHorizontal();
				if(btn_runChecks){
					toggleSettings = false;
					btn_runChecks = false;
					script_runChecks();
				}
			}
			if(checksRan){
				checksRanOnce = true;
				_mainScrollingPosition = EditorGUILayout.BeginScrollView(_mainScrollingPosition);
				script_results();
				EditorGUILayout.EndScrollView();
			}
			if(checksRanOnce && !checksRan){
				GUIAddLabel("Something changed. Run again to see new results.","",TextAnchor.MiddleCenter);
			}
		}
		private void OnEnable(){
			// Default script reload behaviour: External & Primitive values remain. Local references are reset.
			checksRan = false;
		}
		
		// ##################################################################################
		// Main GUI & Functionality

		private void script_runChecks(){
			checksRan = false; checksRunning = true;
			// todo
			checksRan = true; checksRunning = false;
		}

		private void script_results(){

		}

	}
}

#endif
#endif
#endif
