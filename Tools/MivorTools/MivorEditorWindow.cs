// Credits: https://github.com/Jashepp & https://dev.mivor.net/VRChat/
// Version: 0.0.1

#if VRC_SDK_VRCSDK3
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MivorTools.Editor
{
	public partial class BaseWindow<TEditorWindow> : EditorWindow
	{
		protected const string toolName = "Mivor Tools";
		protected const int toolVersion = 0;
		protected const string toolLink = "dev.mivor.net/VRChat";
		protected const string toolLinkFull = "https://dev.mivor.net/VRChat";
		protected const string editorScriptName = "[Placeholder] Script Name";
		protected const string editorWindowName = "[Placeholder] Window Name";
		protected const bool debugLog = true;

		protected static EditorWindow window;
		
		public static void Init(){
			window = EditorWindow.GetWindow(typeof(TEditorWindow),false,editorWindowName);
			window.minSize = new Vector2(300,100);
			window.wantsMouseMove = false;
			window.wantsMouseEnterLeaveWindow = false;
		}

		protected GUIStyle defaultGUIStyle = null;
		protected GUILayoutOption[] GUICustomHandleOptions(GUIContent content, GUIStyle style, object[] options){
			for(int i = 0; i < options.Length; i++){
				if(options[i]!=null && options[i] is string optStr){
					if(optStr=="AutoWidth") options[i] = GUILayout.Width(style.CalcSize(content).x);
					if(optStr=="AutoMaxWidth") options[i] = GUILayout.MaxWidth(style.CalcSize(content).x);
					if(optStr=="AutoMinWidth") options[i] = GUILayout.MinWidth(style.CalcSize(content).x);
				}
				if(options[i]==null || options[i].GetType()!=typeof(GUILayoutOption)) options[i] = GUILayout.ExpandWidth(false);
			}
			return Array.ConvertAll(options,item=>(GUILayoutOption)item);
		}
		protected void GUIAddLabel(string text, string mouseover=null) => GUIAddLabel(text,mouseover,defaultGUIStyle);
		protected void GUIAddLabel(string text, string mouseover=null, GUIStyle style=null, params object[] options){
			if(style==null) style = defaultGUIStyle;
			var content = new GUIContent("<color='#B3B3B3'>"+text+"</color>",mouseover);
			EditorGUILayout.LabelField(content, style, GUICustomHandleOptions(content,style,options));
		}
		protected void GUIAddLabel(string text, string mouseover=null, TextAnchor align=TextAnchor.MiddleLeft,params object[] options){
			var style = new GUIStyle(defaultGUIStyle) { alignment=align };
			var content = new GUIContent("<color='#B3B3B3'>"+text+"</color>",mouseover);
			EditorGUILayout.LabelField(content, style, GUICustomHandleOptions(content,style,options));
		}

	}
}

#endif
#endif
