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
using VRC.SDK3.Avatars.Components;
using IEnumerator = System.Collections.IEnumerator;

namespace MivorTools.Editor
{
	public partial class CheckReferences : BaseWindow<CheckReferences>
	{
		protected new const int toolVersion = 1;
		protected new const string editorScriptName = "Check VRC Avatar References";
		protected new const string editorWindowName = "Check References";
		protected new const bool debugLog = true;

		// Main Menu item
		[MenuItem("Tools/"+toolName+"/"+editorScriptName, false)]
		public new static void Init(){
			BaseWindow<CheckReferences>.Init();
			window.titleContent = new GUIContent(editorWindowName);
		}
		
		// GameObject Menu item
		protected static GameObject menuSelectGameObject = null;
		[MenuItem("GameObject/"+toolName+"/"+editorWindowName+" - Select This Object", false, 21)] // For GameObject, must be 21 or less
		public static void menu_GameObject_checkRefs(){
			if(!window) Init();
			menuSelectGameObject = Selection.activeGameObject;
		}
		[MenuItem("GameObject/"+toolName+"/"+editorWindowName+" - Select This Object", true, 21)]
		public static bool menuValidate_GameObject_checkRefs(){
			if(Selection.activeObject is GameObject) return true;
			return false;
		}

		// Asset Menu item
		protected static UnityObject menuSelectAssetFolder = null;
		[MenuItem("Assets/"+toolName+"/"+editorWindowName+" - Select This Asset", false, 1100)]
		public static void menu_Asset_selectFolder(){
			if(!window) Init();
			if(Selection.objects.Length==0) return;
			if(Selection.objects[0] is GameObject gameObj){
				menuSelectGameObject = (GameObject)gameObj;
			}
			else if(Selection.objects[0] is UnityEditor.DefaultAsset folder){
				menuSelectAssetFolder = (UnityObject)folder;
			}
		}
		[MenuItem("Assets/"+toolName+"/"+editorWindowName+" - Select This Asset", true, 1100)]
		public static bool menuValidate_Asset_selectFolder(){
			if(Selection.objects.Length==0) return false;
			if(Selection.objects[0] is GameObject gameObj) return true;
			else if(Selection.objects[0] is UnityEditor.DefaultAsset folder) return true;
			return false;
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
			if(Event.current.type==EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) Application.OpenURL(toolLink);
			EditorGUILayout.EndHorizontal();
			if(EditorApplication.isPlaying){
				EditorGUILayout.Space(10);
				GUIAddLabel("Please exit <color='#F0F0F0'><b>Play Mode</b></color> to use this script.","",TextAnchor.MiddleCenter);
				checksRan = false;
				return false;
			}
			return true;
		}

		private GameObject mainTargetObject = null;
		private UnityObject mainTargetDir = null;
		private bool toggleSettings = false;
		private bool toggleShowAllResults = true;
		private bool toggleAllowEditing = false;
		private Vector2 _mainScrollingPosition;
		private void script_GUI_Main(){
			bool queueRunCheck = false;
			bool groupsAreChecking = checkGroups.Exists(g=>g.isChecking);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			var newTargetObject = EditorGUILayout.ObjectField("Target Avatar / Object",mainTargetObject,typeof(GameObject),true) as GameObject;
			if(groupsAreChecking && newTargetObject!=mainTargetObject){
				ShowNotification(new GUIContent("Not editable while checking"));
				newTargetObject = mainTargetObject;
			}
			if(!groupsAreChecking){
				if(menuSelectGameObject!=null){
					newTargetObject = menuSelectGameObject;
					menuSelectGameObject = null;
					queueRunCheck = true;
				}
				if(newTargetObject!=null && newTargetObject!=mainTargetObject && AssetDatabase.Contains(newTargetObject)){
					bool hasGameObject = false;
					foreach(GameObject obj in GameObject.FindObjectsOfType(newTargetObject.GetType())){
						if(obj==newTargetObject){ hasGameObject=true; break; }
						GameObject sceneObj = PrefabUtility.GetCorrespondingObjectFromSource(obj);
						if(sceneObj==newTargetObject){ newTargetObject=obj; hasGameObject=true; break; }
					}
					if(!hasGameObject){
						ShowNotification(new GUIContent("Object not within scene"));
						newTargetObject = null;
					}
				}
				if(mainTargetObject==null && newTargetObject==null && (mainTargetDir==null && !menuSelectAssetFolder)){
					EditorGUILayout.EndVertical();
					EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("HelpBox")){ richText=true, alignment=TextAnchor.MiddleCenter, fontSize=defaultGUIStyle.fontSize });
					GUIAddLabel("To <b>continue</b>, select an <b>avatar</b> or <b>object</b>.","",TextAnchor.MiddleCenter);
					GUIAddLabel("It can easily be drag-n-drop'd into the above field.","",TextAnchor.MiddleCenter);
					EditorGUILayout.EndVertical();
					return;
				}
				if(newTargetObject!=mainTargetObject){
					checksRan=false;
					mainTargetObject = newTargetObject;
					if(mainTargetObject!=null && mainTargetDir!=null) mainTargetDir = null;
					if(mainTargetObject!=null && mainTargetDir==null){
						string getAssetPath(Component component,string proName){
							if(component==null) return "";
							object value = null;
							if(component.GetType().GetField(proName)!=null) value = component.GetType().GetField(proName).GetValue(component);
							if(component.GetType().GetProperty(proName)!=null) value = component.GetType().GetProperty(proName).GetValue(component);
							if(value!=null && value is UnityObject @asset && @asset!=null && AssetDatabase.Contains(@asset)){
								return AssetDatabase.GetAssetPath(@asset);
							}
							return "";
						}
						string commonAssetPath(Dictionary<Type,List<string>> toSearch){
							var paths = new List<string>();
							foreach(var item in toSearch){
								var comp = mainTargetObject.GetComponent(item.Key);
								foreach(string propName in item.Value){
									var propValuePath = getAssetPath(comp,propName);
									if(propValuePath.Length>0) paths.Add(propValuePath);
								}
							}
							if(paths.Count==0){
								var assetForObject = PrefabUtility.GetCorrespondingObjectFromSource(mainTargetObject);
								if(assetForObject!=null && AssetDatabase.Contains(assetForObject)){
									paths.Add(AssetDatabase.GetAssetPath(assetForObject));
								}
							}
							return stringsWhereCommon(paths);
						}
						string path = commonAssetPath(new Dictionary<Type,List<string>>(){
							{ typeof(Animator), new List<string>{ "runtimeAnimatorController", "avatar" } },
							{ typeof(VRCAvatarDescriptor), new List<string>{ "expressionsMenu", "expressionParameters" } },
							{ typeof(SkinnedMeshRenderer), new List<string>{ "sharedMesh" } }
						});
						if(path.Length>0) mainTargetDir = AssetDatabase.LoadAssetAtPath(path.Substring(0,path.LastIndexOf("/")),typeof(UnityEditor.DefaultAsset));
					}
				}
			}
			var newTargetDir = EditorGUILayout.ObjectField("Avatar Project Folder",mainTargetDir,typeof(UnityEditor.DefaultAsset),true) as UnityObject;
			if(groupsAreChecking && newTargetDir!=mainTargetDir){
				ShowNotification(new GUIContent("Not editable while checking"));
				newTargetDir = mainTargetDir;
			}
			if(!groupsAreChecking){
				if(menuSelectAssetFolder!=null){
					newTargetDir = menuSelectAssetFolder;
					menuSelectAssetFolder = null;
					if(newTargetDir!=null) queueRunCheck = true;
				}
				if(newTargetDir!=mainTargetDir){
					checksRan=false;
					mainTargetDir = newTargetDir;
					string dirPath = AssetDatabase.GetAssetPath(mainTargetDir);
					if(mainTargetDir!=null && (dirPath==null || dirPath=="" || !AssetDatabase.IsValidFolder(dirPath))){
						ShowNotification(new GUIContent("Not a valid folder"));
						mainTargetDir = null;
					}
				}
			}
			if(mainTargetDir!=null){
				string dirPath = AssetDatabase.GetAssetPath(mainTargetDir);
				var path = (dirPath+"/").Replace("/","{{-slash}}").Replace("{{-slash}}","<color='#FFFFFF'><b> / </b></color>");
				GUIAddLabel("Path: "+path+"",path,new GUIStyle(){ richText=true, alignment=TextAnchor.MiddleLeft, fontSize=defaultGUIStyle.fontSize, wordWrap=true, padding=new RectOffset(2,2,0,0) });
			}
			Rect guiRect = EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("HelpBox")){ richText=true, alignment=TextAnchor.MiddleLeft, fontSize=defaultGUIStyle.fontSize });
			toggleSettings = EditorGUILayout.Toggle(toggleSettings, EditorStyles.foldout, GUILayout.MaxWidth(15.0f));
			GUIAddLabel("Settings");
			toggleSettings = GUI.Toggle(guiRect, toggleSettings, GUIContent.none, new GUIStyle());
			EditorGUILayout.EndHorizontal();
			if(toggleSettings){
				EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(10,0,0,0) });
				toggleShowAllResults = EditorGUILayout.Toggle("Show All Results",toggleShowAllResults);
				toggleAllowEditing = EditorGUILayout.Toggle("Allow Editing",toggleAllowEditing);
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndVertical();
			if(!mainTargetObject || !mainTargetDir){
				//EditorGUILayout.HelpBox("Both object and folder must be selected to continue.",MessageType.None);
				EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("HelpBox")){ richText=true, alignment=TextAnchor.MiddleCenter, fontSize=defaultGUIStyle.fontSize });
				if(mainTargetObject && !mainTargetDir) GUIAddLabel("To <b>continue</b>, select a <b>folder</b>.","",TextAnchor.MiddleCenter);
				else if(!mainTargetObject && mainTargetDir) GUIAddLabel("To <b>continue</b>, select an <b>object</b>.","",TextAnchor.MiddleCenter);
				else GUIAddLabel("To <b>continue</b>, select an <b>object</b> and <b>folder</b>.","",TextAnchor.MiddleCenter);
				EditorGUILayout.EndVertical();
				return;
			}
			if(groupsAreChecking){
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
		
		// ##################################################################################
		// Main GUI & Functionality

		private bool checksRanOnce = false;
		private bool checksRan = false;
		private bool checkKeepRunning = false;
		private void OnEnable(){
			// Default script reload behaviour: External & Primitive values remain. Local references are reset.
			checksRan = false;
			// async void test(){
			// 	Debug.Log("Test Inner Start");
			// 	//var awaitResult = await Promise.Resolve("promiseValue").Then((v)=>{ Debug.Log("Test Inner Mid: "+v); return Promise.Reject(v); });
			// 	//var awaitResult = await Promise.Resolve("promiseValue").Then((v)=>{ Debug.Log("Test Inner Mid: "+v); return v; }) as Promise.PromiseDetached;
			// 	var awaitResult = await Promise.Resolve("promiseValue").Then((v)=>{ Debug.Log("Test Inner Mid: "+v); return v; });
			// 	//var awaitResult = await Promise.Resolve("promiseValue");
			// 	Debug.Log("Test Inner End: "+awaitResult);
			// };
			// Debug.Log("Test Outer Start");
			// test();
			// Debug.Log("Test Outer End");
		}

		private class checkGroup {
			public int id { get; set; }
			public string name { get; set; }
			public bool isChecking = false;
			public bool hasResults = false;
			public bool hasWarnings = false;
			public bool toggleShow = false;
			public string checkFn { get; set; }
			public string resultsFn { get; set; }
			public List<checkResult> results = new List<checkResult>();
		}  
		private checkGroup checkGroupById(int id) => checkGroups.Find(group => group.id==id);
		private void script_results_displayFoldout(checkGroup group,anonFunc resultsFunc){
			EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,3,0), padding=new RectOffset(0,0,0,0) });
			Rect guiRect = EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.box){ alignment=TextAnchor.MiddleLeft });
			group.toggleShow = EditorGUILayout.Toggle(group.toggleShow, EditorStyles.foldout, GUILayout.MaxWidth(15.0f));
			if(group.isChecking){ GUIAddLabel("<color='#F0F0F0'><b>"+group.name+"</b></color> - <color='#F3F3F3'>Checking...</color>"); }
			else if(group.results.Count==0){ GUIAddLabel("<color='#F0F0F0'><b>"+group.name+"</b></color> - <color='#F3E9A9'>No Results</color>"); }
			else if(group.hasWarnings){ GUIAddLabel("<color='#F0F0F0'><b>"+group.name+"</b></color> - <color='#FFB0B0'>Potential Issues</color>"); }
			else { GUIAddLabel("<color='#F0F0F0'><b>"+group.name+"</b></color> - <color='#B0FFB0'>OK</color>"); }
			group.toggleShow = GUI.Toggle(guiRect, group.toggleShow, GUIContent.none, new GUIStyle());
			EditorGUILayout.EndHorizontal();
			if(group.toggleShow){ //  && group.hasResults
				EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(10,0,0,0) });
				resultsFunc();
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndVertical();
		}
		
		private delegate IEnumerator groupCoroutineFunc(checkGroup group);
		private void invokeGroupMethod(checkGroup group,string methodName) { // {0}=id, {1}=name
			var method = this.GetType().GetMethod(String.Format(methodName,group.id,group.name), BindingFlags.Instance | BindingFlags.NonPublic);
			if(method.GetRuntimeBaseDefinition().ReturnType==typeof(IEnumerator)){
				groupCoroutineFunc func = System.Delegate.CreateDelegate(typeof(groupCoroutineFunc),this,method.Name) as groupCoroutineFunc;
				StartCoroutine(func(group)); // method cant have arguments
			}
			else method.Invoke(this, new checkGroup[]{ group });
		}
		private void script_runChecks(){
			checksRan = false; checkKeepRunning = true;
			foreach (checkGroup group in checkGroups){
				invokeGroupMethod(group,group.checkFn);
			}
			checksRan = true;
		}
		private void script_results(){
			if(!checksRan){ return; }
			foreach (checkGroup group in checkGroups){
				script_results_displayFoldout(group,()=>{ invokeGroupMethod(group,group.resultsFn); });
			}
		}

		//private const string defaultCheckFn = "script_group{0}_check";
		//private const string defaultResultsFn = "script_group{0}_results";
		private List<checkGroup> checkGroups = new List<checkGroup>(){
			new checkGroup{ id=1, name="Components", checkFn="script_components_runCheck", resultsFn="script_components_displayResults" },
			new checkGroup{ id=2, name="Assets", checkFn="script_assets_runCheck", resultsFn="script_assets_displayResults" },
			new checkGroup{ id=3, name="Constraints", checkFn="script_constraints_runCheck", resultsFn="script_constraints_displayResults" },
			new checkGroup{ id=4, name="[WIP] Expression Menus", checkFn="script_vrcParams_runCheck", resultsFn="script_vrcParams_displayResults" },
			new checkGroup{ id=5, name="[WIP] Animations", checkFn="script_animations_runCheck", resultsFn="script_animations_displayResults" }
		};

		private class checkResult {
			public bool hasWarning = false;
			public List<string> warningMessages = null;
			public checkResultComponent componentResult { get; set; }
			public checkResultConstraint constraintResult { get; set; }
			public checkResultAsset assetResult { get; set; }
		}
		private class checkResultComponent {
			public Component component { get; set; }
			public getObjectPropsOfTypes.Result propResult { get; set; }
		}
		private class checkResultConstraint {
			public UnityEngine.Animations.ConstraintSource source { get; set; }
			public UnityEngine.Animations.IConstraint constraint { get; set; }
			public Component sourceComponent { get; set; }
			public int sourceIndex = 0;
			public object originalGameObj = null;
			public object originalSource = null;
		}
		private class checkResultAsset {
			public Component component { get; set; }
			public getObjectPropsOfTypes.Result propResult { get; set; }
			public UnityObject assetObject { get; set; }
			public string checkedAssetPath { get; set; }
		}

		private class getObjectPropsOfTypes {
			public bool debugLog = !true;
			public bool debugLogWarning = !true;
			public bool debugLogError = true;
			private void log(object msg){ if(debugLog) Debug.Log(msg); }
			private void logWarning(object msg){ if(debugLogWarning) Debug.LogWarning(msg); }
			private void logError(object msg){ if(debugLogError) Debug.LogError(msg); }

			public delegate bool continueRunningFn();
			public continueRunningFn continueRunningCallback = null;
			
			public object targetObject = null;
			public List<Type> typesSearch = null;
			public List<Type> typesBlacklist = null;
			public Dictionary<Type, List<string>> propBlacklist = null;
			public List<string> propNameBlacklist = null;
			public getObjectPropsOfTypes(object targetObject=null, List<Type> typesSearch=null, List<Type> typesBlacklist=null, Dictionary<Type, List<string>> propBlacklist=null, List<string> propNameBlacklist=null){
				this.targetObject = targetObject;
				this.typesSearch = typesSearch;
				if(typesBlacklist!=null) this.typesBlacklist = typesBlacklist;
				else this.typesBlacklist = new List<Type>(){ typeof(System.ValueType), typeof(SystemObject) };
				if(propBlacklist!=null) this.propBlacklist = propBlacklist;
				else this.propBlacklist = new Dictionary<Type, List<string>>();
				if(propNameBlacklist!=null) this.propNameBlacklist = propNameBlacklist;
				else this.propNameBlacklist = new List<string>(){ "gameObject" };
			}

			public delegate bool cbResult(Result r);
			public delegate void cbResults(List<Result> r);
			public cbResult onNewResult = (Result r)=>true;
			public cbResults onCompletedResults = (List<Result> r)=>{};
			public List<Result> results = new List<Result>();
			public class Result {
				public string name = null;
				public bool isArray = false;
				public bool isList = false;
				public Type type = null;
				public Type typeArray = null;
				public Type typeList = null;
				public Type checkedType = null;
				public PropertyInfo propInfo = null;
				public FieldInfo fieldInfo = null;
				public object parentObj = null;
				public object checkedValue = null;
				public object[] checkedValuesArray = null;
				public List<object> checkedValuesList = null;
				public object[] getValuesArray(){
					if(propInfo!=null && typeArray!=null && type.IsArray) return (object[])propInfo.GetValue(parentObj);
					if(fieldInfo!=null && typeArray!=null && type.IsArray) return (object[])fieldInfo.GetValue(parentObj);
					if(propInfo!=null && typeList!=null) return getValuesList()?.ToArray();
					if(fieldInfo!=null && typeList!=null) return getValuesList()?.ToArray();
					return null;
				}
				private List<object> useListValue(SystemObject obj){
					if(obj==null) return null;
					return (List<object>)(obj as IList<object>);
				}
				public List<object> getValuesListCloned(){
					var obj = getValuesList();
					return obj==null ? null : new List<object>(obj);
				}
				public List<object> getValuesList(){
					if(propInfo!=null && typeArray!=null && type.IsArray) return new List<object>(getValuesArray());
					if(fieldInfo!=null && typeArray!=null && type.IsArray) return new List<object>(getValuesArray());
					if(propInfo!=null && typeList!=null) return useListValue(propInfo.GetValue(parentObj,null));
					if(fieldInfo!=null && typeList!=null) return useListValue(fieldInfo.GetValue(parentObj));
					return null;
				}
				public object getValue(){
					if(isArray || isList) return null;
					if(propInfo!=null) return propInfo.GetValue(parentObj);
					else if(fieldInfo!=null) return fieldInfo.GetValue(parentObj);
					return null;
				}
				public void setValue(object value){
					if(isArray || isList) return;
					if(propInfo!=null) propInfo.SetValue(parentObj,value);
					else if(fieldInfo!=null) fieldInfo.SetValue(parentObj,value);
				}
				public bool isWritable(){
					if(propInfo!=null && !propInfo.CanWrite) return true;
					if(fieldInfo!=null && !fieldInfo.IsInitOnly && !fieldInfo.IsLiteral) return true;
					return false;
				}
			}

			public checkpointTiming runTimer = null;
			public IEnumerator run(){
				if(targetObject==null || typesSearch==null || propBlacklist==null){ yield break; }
				if(runTimer==null){ checkpointTiming runTimer = new checkpointTiming(); }
				yield return null;
				if(continueRunningCallback!=null && !continueRunningCallback()) yield break;

				log(targetObject);
				List<string> checkedProps = new List<string>();
				foreach(PropertyInfo prop in targetObject.GetType().GetProperties(BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.OptionalParamBinding | BindingFlags.InvokeMethod)){
					if(continueRunningCallback!=null && !continueRunningCallback()) yield break;
					if(runTimer.checkpoint()) yield return null;
					var ignoreTypes = new List<Type>();
					if(loopThroughPropTypes(targetObject,prop,prop.PropertyType,ignoreTypes,"PropertyType - top")){ continue; }
					//if(loopThroughPropTypes(targetObject,prop,prop.DeclaringType,checkedTypes,"DeclaringType - top")){ continue; }
					//if(loopThroughPropTypes(targetObject,prop,prop.ReflectedType,ignoreTypes,"ReflectedType - top")){ continue; }
				}
				foreach(FieldInfo field in targetObject.GetType().GetFields(BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.OptionalParamBinding | BindingFlags.InvokeMethod)){
					if(continueRunningCallback!=null && !continueRunningCallback()) yield break;
					if(runTimer.checkpoint()) yield return null;
					if(checkedProps.Contains(field.Name)){ continue; }
					var ignoreTypes = new List<Type>();
					if(loopThroughFieldTypes(targetObject,field,field.FieldType,ignoreTypes,"FieldType - top")){ continue; }
					//if(loopThroughFieldTypes(targetObject,field,field.DeclaringType,ignoreTypes,"DeclaringType - top")){ continue; }
					//if(loopThroughFieldTypes(targetObject,field,field.ReflectedType,ignoreTypes,"ReflectedType - top")){ continue; }
				}

				bool loopThroughPropTypes(object obj, PropertyInfo prop, Type propType, List<Type> ignoreTypes, string debugInfo){
					var logInfo = "\""+prop.Name+"\" ("+propType+") on: "+targetObject+" ["+debugInfo+"]";
					if(typesBlacklist.Contains(propType)){
						logWarning("### Blacklisted Prop Type: "+logInfo);
						return false;
					}
					if(propNameBlacklist.Contains(prop.Name) || (propBlacklist.ContainsKey(propType) && propBlacklist[propType].Contains(prop.Name))){
						logWarning("### Blacklisted Prop Name: "+logInfo);
						return true;
					}
					if(ignoreTypes.Contains(propType)){
						logWarning("### Ignored Prop Type: "+logInfo+" --- PropertyType: "+prop.PropertyType.FullName+" DeclaringType: "+prop.DeclaringType.FullName+" ReflectedType: "+prop.ReflectedType.FullName+"");
						return false;
					}
					Type propType2 = propType;
					if(propType.IsArray){ propType2 = propType.GetElementType(); }
					foreach(Type type in propType.GetInterfaces()) {
						if(type.IsGenericType && type.GetGenericTypeDefinition()==typeof(IList<>)){
							propType2 = type.GetGenericArguments()[0]; break;
						}
						//if(type==typeof(IList<>)){ propType2 = typeof(object); break; }
					}
					bool validType = false; Type checkedType = null;
					foreach(Type type in typesSearch){
						if( propType==type
							|| propType.IsAssignableFrom(type) || type.IsAssignableFrom(propType)
							|| propType.IsEquivalentTo(type) || type.IsEquivalentTo(propType)
							|| propType.IsSubclassOf(type) || type.IsSubclassOf(propType)
							|| propType.Equals(type) || type.Equals(propType)
							|| propType.IsInstanceOfType(type) || type.IsInstanceOfType(propType)
						){ validType=true; checkedType = type; break; }
						if( propType2!=propType && propType2==type
							|| propType2.IsAssignableFrom(type) || type.IsAssignableFrom(propType2)
							|| propType2.IsEquivalentTo(type) || type.IsEquivalentTo(propType2)
							|| propType2.IsSubclassOf(type) || type.IsSubclassOf(propType2)
							|| propType2.Equals(type) || type.Equals(propType2)
							|| propType2.IsInstanceOfType(type) || type.IsInstanceOfType(propType2)
						){ validType=true; checkedType = type; break; }
					}
					if(!validType){
						ignoreTypes.Add(propType);
						logWarning("### Ignoring Prop Type: "+logInfo);
						//if(!validType && propType.BaseType is object && propType.BaseType!=propType){ validType = loopThroughPropTypes(obj,prop,propType.BaseType,ignoreTypes,"BaseType - recursive"); }
						//if(!validType && propType.ReflectedType is object && propType.ReflectedType!=propType){ validType = loopThroughPropTypes(obj,prop,propType.ReflectedType,ignoreTypes,"ReflectedType - recursive"); }
						//if(!validType && propType.DeclaringType is object && propType.DeclaringType!=propType){ validType = loopThroughPropTypes(obj,prop,propType.DeclaringType,ignoreTypes,"DeclaringType - recursive"); }
						//if(!validType && propType.UnderlyingSystemType is object && propType.UnderlyingSystemType!=propType){ validType = loopThroughPropTypes(obj,prop,propType.UnderlyingSystemType,ignoreTypes,"UnderlyingSystemType - recursive"); }
						return validType;
					}
					if(prop.GetCustomAttributes(typeof(System.ObsoleteAttribute),true).Length>0){
						//logWarning("### Deprecated Property: "+logInfo);
						return false;
					}
					try{
						var newResult = new Result { name=prop.Name, type=propType, checkedType=checkedType, propInfo=prop, parentObj=obj };
						if(propType2!=propType && propType.IsArray){
							newResult.isArray = true;
							newResult.typeArray = propType2;
							newResult.checkedValuesArray = (object[])newResult.getValuesArray().Clone();
						}
						else if(propType2!=propType){
							newResult.isList = true;
							newResult.typeList = propType2;
							newResult.checkedValuesList = newResult.getValuesListCloned();
						}
						else newResult.checkedValue = newResult.getValue();
						//if(prop.Name=="material"){ Debug.Log("Prop - "+prop.Name+" - "+propType+" - "+checkedType); }
						log("--- Valid Prop: "+logInfo);
						checkedProps.Add(prop.Name);
						if(onNewResult(newResult)){ results.Add(newResult); }
					}catch(Exception e){
						if(e is UnityEngine.UnassignedReferenceException) return false;
						logError("### Exception on PropertyInfo - newResult or onNewResult: "+logInfo+" "+propType2);
						Debug.LogException(e);
						return false;
					}
					return true;
				}

				bool loopThroughFieldTypes(object obj, FieldInfo field, Type fieldType, List<Type> ignoreTypes, string debugInfo){
					var logInfo = "\""+field.Name+"\" ("+fieldType+") on: "+targetObject+" ["+debugInfo+"]";
					if(typesBlacklist.Contains(fieldType)){
						logWarning("### Blacklisted Field Type: "+logInfo);
						return false;
					}
					if(propNameBlacklist.Contains(field.Name) || (propBlacklist.ContainsKey(fieldType) && propBlacklist[fieldType].Contains(field.Name))){
						logWarning("### Blacklisted Field Name: "+logInfo);
						return true;
					}
					if(ignoreTypes.Contains(fieldType)){
						logWarning("### Ignored Field Type: "+logInfo+" --- FieldType: "+field.FieldType.FullName+" DeclaringType: "+field.DeclaringType.FullName+" ReflectedType: "+field.ReflectedType.FullName+"");
						return false;
					}
					Type propType2 = fieldType;
					if(fieldType.IsArray){ propType2 = fieldType.GetElementType(); }
					foreach(Type type in fieldType.GetInterfaces()) {
						if(type.IsGenericType && type.GetGenericTypeDefinition()==typeof(IList<>)){
							propType2 = type.GetGenericArguments()[0]; break;
						}
						//if(type==typeof(IList<>)){ propType2 = typeof(object); break; }
					}
					bool validType = false; Type checkedType = null;
					foreach(Type type in typesSearch){
						if(
							fieldType==type
							|| fieldType.IsAssignableFrom(type) || type.IsAssignableFrom(fieldType)
							|| fieldType.IsEquivalentTo(type) || type.IsEquivalentTo(fieldType)
							|| fieldType.IsSubclassOf(type) || type.IsSubclassOf(fieldType)
							|| fieldType.Equals(type) || type.Equals(fieldType)
							|| fieldType.IsInstanceOfType(type) || type.IsInstanceOfType(fieldType)
						){ validType=true; checkedType = type; break; }
						if( propType2!=fieldType && propType2==type
							|| propType2.IsAssignableFrom(type) || type.IsAssignableFrom(propType2)
							|| propType2.IsEquivalentTo(type) || type.IsEquivalentTo(propType2)
							|| propType2.IsSubclassOf(type) || type.IsSubclassOf(propType2)
							|| propType2.Equals(type) || type.Equals(propType2)
							|| propType2.IsInstanceOfType(type) || type.IsInstanceOfType(propType2)
						){ validType=true; checkedType = type; break; }
					}
					if(!validType){
						ignoreTypes.Add(fieldType);
						logWarning("### Ignoring Field Type: "+logInfo);
						if(!validType && fieldType.BaseType is object && fieldType.BaseType!=fieldType){ validType = loopThroughFieldTypes(obj,field,fieldType.BaseType,ignoreTypes,"BaseType - recursive"); }
						//if(!validType && fieldType.ReflectedType is object && fieldType.ReflectedType!=fieldType){ validType = loopThroughFieldTypes(obj,field,fieldType.ReflectedType,ignoreTypes,"ReflectedType - recursive"); }
						//if(!validType && fieldType.DeclaringType is object && fieldType.DeclaringType!=fieldType){ validType = loopThroughFieldTypes(obj,field,fieldType.DeclaringType,ignoreTypes,"DeclaringType - recursive"); }
						//if(!validType && fieldType.UnderlyingSystemType is object && fieldType.UnderlyingSystemType!=fieldType){ validType = loopThroughFieldTypes(obj,field,fieldType.UnderlyingSystemType,ignoreTypes,"UnderlyingSystemType - recursive"); }
						return validType;
					}
					if(field.GetCustomAttributes(typeof(System.ObsoleteAttribute),true).Length>0){
						//logWarning("### Deprecated Field: "+logInfo);
						return false;
					}
					try{
						var newResult = new Result { name=field.Name, type=fieldType, typeList=propType2, checkedType=checkedType, fieldInfo=field, parentObj=obj };
						if(propType2!=fieldType && fieldType.IsArray){
							newResult.isArray = true;
							newResult.typeArray = propType2;
							newResult.checkedValuesArray = (object[])newResult.getValuesArray().Clone();
						}
						else if(propType2!=fieldType){
							newResult.isList = true;
							newResult.typeList = propType2;
							newResult.checkedValuesList = newResult.getValuesListCloned();
						}
						else newResult.checkedValue = newResult.getValue();
						log("--- Valid Field: "+logInfo);
						checkedProps.Add(field.Name);
						if(onNewResult(newResult)){ results.Add(newResult); }
					}catch(Exception e){
						if(e is UnityEngine.UnassignedReferenceException) return false;
						logError("### Exception on FieldInfo - newResult or onNewResult: "+logInfo+" "+propType2+"");
						Debug.LogException(e);
						return false;
					}
					return true;
				}

				if(continueRunningCallback!=null && !continueRunningCallback()) yield break;
				yield return null;
				onCompletedResults(results);
				yield return null;
				if(runTimer.checkpoint()) yield return null;
			}
		}

		// ##################################################################################

		private IEnumerator script_components_runCheck(checkGroup group){
			checkpointTiming runTimer = new checkpointTiming();
			var results = group.results = new List<checkResult>();
			group.hasResults = group.hasWarnings = false; group.isChecking = true;
			yield return null;

			List<GameObject> uniqueGameObjects = new List<GameObject>();
			List<Component> uniqueComponents = new List<Component>();
			
			Component[] components = mainTargetObject.GetComponentsInChildren<Component>(true);
			foreach(Component component in components){
				if(!uniqueComponents.Contains(component)) uniqueComponents.Add(component);
				if(!uniqueGameObjects.Contains(component.gameObject)) uniqueGameObjects.Add(component.gameObject);
			}
			
			//Debug.Log("group1 - 1 - script_components_runCheck");
			var promiseChain = new Promise.PromiseChain();
			foreach (Component component in uniqueComponents){
				if(runTimer.checkpoint()) yield return null;
				if(!checkKeepRunning) yield break;
				getObjectPropsOfTypes getProps = new getObjectPropsOfTypes(){
					debugLog = !true,
					debugLogWarning = !true,
					runTimer = runTimer,
					targetObject = component,
					continueRunningCallback = ()=>{ return checkKeepRunning; },
					typesSearch = new List<Type>{ // VRCExpressionsMenu VRCExpressionParameters
						typeof(PhysicMaterial), typeof(Transform), typeof(Mesh), typeof(Avatar), typeof(RuntimeAnimatorController),
						typeof(UnityEngine.Animator), typeof(UnityEngine.AnimationClip), typeof(UnityEngine.MeshFilter), typeof(UnityEngine.Material), typeof(UnityEngine.SkinnedMeshRenderer), typeof(UnityEngine.MeshRenderer), typeof(UnityEngine.Renderer), typeof(UnityEngine.Texture2D), typeof(UnityEngine.Shader), typeof(UnityEngine.Collider), typeof(UnityEngine.TextAsset), typeof(UnityEditor.DefaultAsset),
						typeof(VRC.SDK3.Avatars.Components.VRCStation), typeof(VRC.SDKBase.VRCStation), typeof(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor), typeof(VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone),
						typeof(UnityEngine.GameObject), typeof(UnityObject)
					},
					propNameBlacklist = new List<string>(){ "gameObject" },
					propBlacklist = new Dictionary<Type, List<string>>(){
						{ typeof(Transform), new List<string>{ "parent", "root", "transform", "gameObject" } },
						{ typeof(Mesh), new List<string>{ "mesh" } }, // sharedMesh is used instead
						{ typeof(UnityEngine.MeshFilter), new List<string>{ "mesh" } },
						{ typeof(UnityEngine.Material), new List<string>{ "material","materials" } }, // sharedMaterial is used instead
						{ typeof(UnityEngine.Renderer), new List<string>{ "material","materials" } },
						{ typeof(UnityEngine.MeshRenderer), new List<string>{ "material","materials" } },
						{ typeof(UnityEngine.SkinnedMeshRenderer), new List<string>{ "material","materials" } },
						{ typeof(VRC.SDK3.Avatars.Components.VRCStation), new List<string>{ "gameObject", "name" } }
					},
					typesBlacklist = new List<Type>(){ typeof(System.ValueType), typeof(SystemObject), component.GetType() },
					onNewResult = (getObjectPropsOfTypes.Result propResult)=>{
						List<string> warnings = new List<string>();
						var propValue = propResult.checkedValue;
						//Debug.Log("Prop: "+propResult.name+"=\""+(propValue ?? "null") +"\" on "+component);
						if(propValue!=null && propValue is Component @comp && @comp.gameObject!=null &&  @comp.gameObject is UnityEngine.GameObject @object && !uniqueGameObjects.Contains(@object)){
							warnings.Add("Object not within target object.");
						}
						// Update Results
						checkResult newResult = new checkResult(){
							hasWarning = warnings.Count>0,
							warningMessages = warnings,
							componentResult = new checkResultComponent(){
								component = component,
								propResult = propResult
							}
						};
						group.results.Add(newResult);
						// Update checkGroup
						group.hasResults = results.Count>0;
						group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
						return true;
					},
					onCompletedResults = (List<getObjectPropsOfTypes.Result> propResults)=>{  }
				};
				if(getProps.run() is IEnumerator routine){
					promiseChain = promiseChain.Chain(Promise.StartCoroutine(routine));
					yield return routine;
				}
			}
			//Debug.Log("group1 - 3 - script_components_runCheck");
			promiseChain.Then((v)=>{
				//Debug.Log("group1 - 4 - All Promises Resolved");
				group.isChecking = false;
				group.hasResults = results.Count>0;
				group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			});
		}

		private void script_components_displayResults(checkGroup group){
			var results = group.results; bool resultsShown = false;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			List<GameObject> uniqueGameObjects = new List<GameObject>();
			foreach(Component component in mainTargetObject.GetComponentsInChildren<Component>(true)){
				if(!uniqueGameObjects.Contains(component.gameObject)) uniqueGameObjects.Add(component.gameObject);
			}
			EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(0,0,0,0) });
			foreach(checkResult result in results){
				checkResultComponent componentResult = result.componentResult;
				Component component = componentResult.component;
				getObjectPropsOfTypes.Result propResult = componentResult.propResult;
				var originalValue = (UnityObject)propResult.checkedValue;
				var currentValue = (UnityObject)propResult.getValue();
				var newObject = currentValue;
				if(!toggleShowAllResults && !(result.hasWarning || originalValue!=currentValue)) continue;
				EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.box){ margin=new RectOffset(0,0,10,10), padding=new RectOffset(0,0,0,0) });
				// Objects
				var of1 = EditorGUILayout.ObjectField(new GUIContent(component.GetType().Name,"Component: "+component.name),component,typeof(Component),true);
				if(of1!=component) ShowNotification(new GUIContent("Not Editable"));
				if(originalValue==currentValue){
					newObject = (UnityObject)EditorGUILayout.ObjectField(new GUIContent(propResult.name,propResult.type.Name+"."+propResult.name),originalValue,propResult.type,true);
				}
				else {
					GUIAddLabel("<b>(Edited)</b> "+propResult.name+":",propResult.type.Name+"."+propResult.name);
					EditorGUILayout.ObjectField("Checked Component: ",originalValue,propResult.type,true);
					newObject = (UnityObject)EditorGUILayout.ObjectField("Current Component: ",currentValue,propResult.type,true);
				}
				if(newObject!=currentValue){
					if(propResult.isWritable()){
						ShowNotification(new GUIContent("Property \""+propResult.name+"\" cannot be edited"));
						newObject = currentValue;
					}
					if(group.isChecking || !toggleAllowEditing){
						ShowNotification(new GUIContent("Editing is currently disabled"));
						newObject = currentValue;
					}
				}
				// Edit & Actions
				if(!group.isChecking && toggleAllowEditing && currentValue!=newObject){
					Debug.Log("Edited component value. From: "+currentValue+" To: "+newObject);
					propResult.setValue(newObject);
					currentValue = newObject;
				}
				if(originalValue!=currentValue){
					EditorGUILayout.BeginHorizontal();
					var btnReset = GUILayout.Button(new GUIContent("Undo / Reset to checked component","From: "+currentValue+" To: "+originalValue));
					EditorGUILayout.EndHorizontal();
					if(btnReset && currentValue!=originalValue){
						btnReset = false;
						Debug.Log("Undo / Reset component value. From: "+currentValue+" To: "+originalValue);
						propResult.setValue(originalValue);
						currentValue = originalValue;
					}
				}
				// Warnings
				result.warningMessages.Clear();
				if(currentValue!=null && currentValue is Component @comp && @comp.gameObject!=null && @comp.gameObject is UnityEngine.GameObject @object && !uniqueGameObjects.Contains(@object)){
					var warning = "Object not within target object.";
					result.warningMessages.Add(null);
					GUIAddLabel("<color='#FFB0B0'><b>Warning:</b></color> <color='#F3E9A9'>"+warning+"</color>","",new GUIStyle(){ margin=new RectOffset(0,0,5,0) });
					var of = EditorGUILayout.ObjectField(new GUIContent("GameObject",propResult.type.Name+"."+propResult.name+".gameObject"),@object,typeof(UnityEngine.GameObject),true);
					if(of!=@object) ShowNotification(new GUIContent("Edit the \""+propResult.name+"\" field above instead"));
				}
				result.hasWarning = result.warningMessages.Count>0;
				if(result.hasWarning && result.warningMessages.Exists(w=>w!=null)){
					EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,5,0), padding=new RectOffset(0,0,0,0) });
					foreach(string warning in result.warningMessages){
						if(warning!=null) GUIAddLabel("<color='#FFB0B0'><b>Warning:</b></color> <color='#F3E9A9'>"+warning+"</color>");
					}
					EditorGUILayout.EndVertical();
				}
				EditorGUILayout.EndVertical();
				resultsShown = true;
			}
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			if(group.isChecking) GUIAddLabel("Checking...");
			else if(results.Count==0) GUIAddLabel("No Results");
			else if(!resultsShown) GUIAddLabel("Everything seems OK.");
			EditorGUILayout.EndVertical();
		}

		// ##################################################################################

		private IEnumerator script_assets_runCheck(checkGroup group){
			checkpointTiming runTimer = new checkpointTiming();
			var results = group.results = new List<checkResult>();
			group.hasResults = group.hasWarnings = false; group.isChecking = true;
			yield return null;

			List<GameObject> uniqueGameObjects = new List<GameObject>();
			List<Component> uniqueComponents = new List<Component>();
			
			Component[] components = mainTargetObject.GetComponentsInChildren<Component>(true);
			foreach(Component component in components){
				if(!uniqueComponents.Contains(component)) uniqueComponents.Add(component);
				GameObject childObj = component.gameObject;
				if(!uniqueGameObjects.Contains(childObj)) uniqueGameObjects.Add(childObj);
			}
			
			var promiseChain = new Promise.PromiseChain();
			foreach (Component component in uniqueComponents){
				if(runTimer.checkpoint()) yield return null;
				if(!checkKeepRunning) yield break;
				getObjectPropsOfTypes getProps = new getObjectPropsOfTypes(){
					debugLog = !true,
					debugLogWarning = !true,
					runTimer = runTimer,
					targetObject = component,
					continueRunningCallback = ()=>{ return checkKeepRunning; },
					typesSearch = new List<Type>{
						typeof(PhysicMaterial), typeof(Transform), typeof(Mesh), typeof(Avatar), typeof(RuntimeAnimatorController),
						typeof(UnityEngine.Animator), typeof(UnityEngine.AnimationClip), typeof(UnityEngine.MeshFilter), typeof(UnityEngine.Material), typeof(UnityEngine.SkinnedMeshRenderer), typeof(UnityEngine.MeshRenderer), typeof(UnityEngine.Renderer), typeof(UnityEngine.Texture2D), typeof(UnityEngine.Shader), typeof(UnityEngine.Collider), typeof(UnityEngine.TextAsset), typeof(UnityEditor.DefaultAsset),
						typeof(VRC.SDK3.Avatars.Components.VRCStation), typeof(VRC.SDKBase.VRCStation), typeof(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor), typeof(VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone), typeof(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu), typeof(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters),
						typeof(UnityEngine.GameObject), typeof(UnityObject)
					},
					propNameBlacklist = new List<string>(){  },
					propBlacklist = new Dictionary<Type, List<string>>(){
						//{ typeof(Transform), new List<string>{ "parent", "root", "transform", "gameObject" } },
						{ typeof(Mesh), new List<string>{ "mesh" } }, // sharedMesh is used instead
						{ typeof(UnityEngine.MeshFilter), new List<string>{ "mesh" } },
						{ typeof(UnityEngine.Material), new List<string>{ "material","materials" } }, // sharedMaterial is used instead ,"materials"
						{ typeof(UnityEngine.Renderer), new List<string>{ "material","materials" } },
						{ typeof(UnityEngine.MeshRenderer), new List<string>{ "material","materials" } },
						{ typeof(UnityEngine.SkinnedMeshRenderer), new List<string>{ "material","materials" } },
						//{ typeof(VRC.SDK3.Avatars.Components.VRCStation), new List<string>{ "gameObject", "name" } }
					},
					typesBlacklist = new List<Type>(){  },
					//typesBlacklist = new List<Type>(){ typeof(System.ValueType), typeof(SystemObject), component.GetType() },
					onNewResult = (getObjectPropsOfTypes.Result propResult)=>{
						List<string> warnings = new List<string>();
						var propValue = propResult.checkedValue;
						string basePath = AssetDatabase.GetAssetPath(mainTargetDir);
						if(propValue is UnityObject @object && AssetDatabase.Contains(@object)){
							string assetPath = AssetDatabase.GetAssetPath(@object);
							if(!assetPath.StartsWith(basePath)){
								warnings.Add("Asset is not within the target folder.");
							}
							// Update Results
							checkResult newResult = new checkResult(){
								hasWarning = warnings.Count>0,
								warningMessages = warnings,
								assetResult = new checkResultAsset(){
									component = component,
									propResult = propResult,
									checkedAssetPath = assetPath
								}
							};
							group.results.Add(newResult);
							// Update checkGroup
							group.hasResults = results.Count>0;
							group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
						}
						return true;
					},
					onCompletedResults = (List<getObjectPropsOfTypes.Result> propResults)=>{  }
				};
				if(getProps.run() is IEnumerator routine){
					promiseChain = promiseChain.Chain(Promise.StartCoroutine(routine));
					yield return routine;
				}
			}
			promiseChain.Then((v)=>{
				group.isChecking = false;
				group.hasResults = results.Count>0;
				group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			});
		}

		private void script_assets_displayResults(checkGroup group){
			var results = group.results; bool resultsShown = false;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(0,0,0,0) });
			foreach(checkResult result in results){
				checkResultAsset assetResult = result.assetResult;
				Component component = assetResult.component;
				getObjectPropsOfTypes.Result propResult = assetResult.propResult;
				var originalValue = (UnityObject)propResult.checkedValue;
				var currentValue = (UnityObject)propResult.getValue();
				string checkedAssetPath = assetResult.checkedAssetPath;
				//string originalAssetPath = AssetDatabase.GetAssetPath(originalValue);
				string currentAssetPath = AssetDatabase.GetAssetPath(currentValue);
				var newObject = currentValue;
				if(!toggleShowAllResults && !(result.hasWarning || originalValue!=currentValue || checkedAssetPath!=currentAssetPath)) continue;
				EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.box){ margin=new RectOffset(0,0,10,10), padding=new RectOffset(0,0,0,0) });
				// Objects
				var of1 = EditorGUILayout.ObjectField(new GUIContent(component.GetType().Name,"Component: "+component.name),component,typeof(Component),true);
				if(of1!=component) ShowNotification(new GUIContent("Not Editable"));
				if(originalValue==currentValue){
					newObject = (UnityObject)EditorGUILayout.ObjectField(new GUIContent(propResult.name,propResult.type.Name+"."+propResult.name),currentValue,propResult.type,true);
				}
				else {
					GUIAddLabel("<b>(Edited)</b> "+propResult.name+":",propResult.type.Name+"."+propResult.name);
					EditorGUILayout.ObjectField("Checked Asset: ",originalValue,propResult.type,true);
					newObject = (UnityObject)EditorGUILayout.ObjectField("Current Asset: ",currentValue,propResult.type,true);
				}
				if(newObject!=currentValue){
					if(propResult.isWritable()){
						ShowNotification(new GUIContent("Property \""+propResult.name+"\" cannot be edited"));
						newObject = currentValue;
					}
					if(group.isChecking || !toggleAllowEditing){
						ShowNotification(new GUIContent("Editing is currently disabled"));
						newObject = currentValue;
					}
				}
				// Edit
				if(!group.isChecking && toggleAllowEditing && currentValue!=newObject){
					Debug.Log("Edited asset value. From: "+currentValue+" To: "+newObject);
					propResult.setValue(newObject);
					currentValue = newObject;
					currentAssetPath = AssetDatabase.GetAssetPath(currentValue);
				}
				// Warnings, Asset Path, Actions
				result.warningMessages.Clear();
				string basePath = AssetDatabase.GetAssetPath(mainTargetDir);
				if(currentAssetPath.StartsWith(basePath)){
					var common = stringsWhereCommon(new List<string>{ basePath+"/", currentAssetPath });
					common = common.Substring(0,common.LastIndexOf("/"));
					var diff = currentAssetPath.Substring(common.Length);
					common = common.Replace("/","{{-slash}}").Replace("{{-slash}}","<color='#FFFFFF'><b> / </b></color>"); diff = diff.Replace("/","{{-slash}}").Replace("{{-slash}}","<color='#FFFFFF'><b> / </b></color>");
					//GUIAddLabel(""+common+"<color='#B0FFB0'>"+diff+"</color>",currentAssetPath,new GUIStyle(GUI.skin.GetStyle("textArea")){ richText=true, alignment=TextAnchor.MiddleLeft, fontSize=defaultGUIStyle.fontSize, wordWrap=true });
					GUIAddLabel(""+common+"<color='#B0FFB0'>"+diff+"</color>",currentAssetPath,new GUIStyle(){ richText=true, alignment=TextAnchor.MiddleLeft, fontSize=defaultGUIStyle.fontSize, wordWrap=true, padding=new RectOffset(3,3,2,0) });
				}
				else {
					var common = stringsWhereCommon(new List<string>{ basePath+"/", currentAssetPath });
					common = common.Substring(0,common.LastIndexOf("/"));
					var diff = currentAssetPath.Substring(common.Length);
					common = common.Replace("/","{{-slash}}").Replace("{{-slash}}","<color='#FFFFFF'><b> / </b></color>"); diff = diff.Replace("/","{{-slash}}").Replace("{{-slash}}","<color='#FFFFFF'><b> / </b></color>");
					//GUIAddLabel(""+common+"<color='#FFB0B0'>"+diff+"</color>",currentAssetPath,new GUIStyle(GUI.skin.GetStyle("textArea")){ richText=true, alignment=TextAnchor.MiddleLeft, fontSize=defaultGUIStyle.fontSize, wordWrap=true });
					GUIAddLabel(""+common+"<color='#FFB0B0'>"+diff+"</color>",currentAssetPath,new GUIStyle(){ richText=true, alignment=TextAnchor.MiddleLeft, fontSize=defaultGUIStyle.fontSize, wordWrap=true, padding=new RectOffset(3,3,2,0) });
					var warning = "Asset is not within the target folder.";
					result.warningMessages.Add(null);
					GUIAddLabel("<color='#FFB0B0'><b>Warning:</b></color> <color='#F3E9A9'>"+warning+"</color>","",new GUIStyle(){ margin=new RectOffset(0,0,5,0) });
				}
				if(originalValue==currentValue && checkedAssetPath!=currentAssetPath){
					var warning = "Asset has been moved since last checked.";
					result.warningMessages.Add(null);
					GUIAddLabel("<color='#FFB0B0'><b>Warning:</b></color> <color='#F3E9A9'>"+warning+"</color>","",new GUIStyle(){ margin=new RectOffset(0,0,5,0) });
					EditorGUILayout.BeginHorizontal();
					var btnResetPath = GUILayout.Button(new GUIContent("Undo / Move asset to checked path","From: "+currentAssetPath+" To: "+checkedAssetPath));
					EditorGUILayout.EndHorizontal();
					if(btnResetPath){
						btnResetPath = false;
						Debug.Log("Move asset path. From: "+currentAssetPath+" To: "+checkedAssetPath);
						AssetDatabase.MoveAsset(currentAssetPath,checkedAssetPath);
					}
				}
				if(originalValue!=currentValue){
					EditorGUILayout.BeginHorizontal();
					var btnReset = GUILayout.Button(new GUIContent("Undo / Reset to checked asset","From: "+currentValue+" To: "+originalValue));
					EditorGUILayout.EndHorizontal();
					if(btnReset){
						btnReset = false;
						Debug.Log("Undo / Reset asset value. From: "+currentValue+" To: "+originalValue);
						propResult.setValue(originalValue);
						currentValue = originalValue;
						currentAssetPath = AssetDatabase.GetAssetPath(currentValue);
					}
				}
				result.hasWarning = result.warningMessages.Count>0;
				if(result.hasWarning && result.warningMessages.Exists(w=>w!=null)){
					EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,5,0), padding=new RectOffset(0,0,0,0) });
					foreach(string warning in result.warningMessages){
						if(warning!=null) GUIAddLabel("<color='#FFB0B0'><b>Warning:</b></color> <color='#F3E9A9'>"+warning+"</color>","");
					}
					EditorGUILayout.EndVertical();
				}
				EditorGUILayout.EndVertical();
				resultsShown = true;
			}
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			if(group.isChecking) GUIAddLabel("Checking...");
			else if(results.Count==0) GUIAddLabel("No Results");
			else if(!resultsShown) GUIAddLabel("Everything seems OK.");
			EditorGUILayout.EndVertical();
		}

		// ##################################################################################

		private void script_constraints_runCheck(checkGroup group){
			var results = group.results = new List<checkResult>();
			group.hasResults = group.hasWarnings = false; group.isChecking = true;
			
			List<GameObject> uniqueGameObjects = new List<GameObject>();
			List<Component> uniqueComponents = new List<Component>();
			
			Component[] components = mainTargetObject.GetComponentsInChildren<Component>(true);
			foreach(Component component in components){
				if(!uniqueComponents.Contains(component)) uniqueComponents.Add(component);
				if(!uniqueGameObjects.Contains(component.gameObject)) uniqueGameObjects.Add(component.gameObject);
			}
			
			foreach (Component component in uniqueComponents){
				if(component is UnityEngine.Animations.IConstraint constraint){
					// https://docs.unity3d.com/ScriptReference/Animations.IConstraint.GetSources.html
					List<UnityEngine.Animations.ConstraintSource> sources = new List<UnityEngine.Animations.ConstraintSource>();
					constraint.GetSources(sources);
					int sourceIndex = 0;
					foreach (UnityEngine.Animations.ConstraintSource source in sources){
						Component sourceComponent = (Component)source.sourceTransform;
						if(sourceComponent!=null){
							GameObject gameObj = sourceComponent?.gameObject;
							List<string> warnings = new List<string>();
							if(sourceComponent.gameObject!=null && sourceComponent.gameObject is UnityEngine.GameObject @object && !uniqueGameObjects.Contains(@object)){
								warnings.Add("Source Transform Object not within target object.");
							}
							// Update Results
							checkResult newResult = new checkResult(){
								hasWarning = warnings.Count>0,
								warningMessages = warnings,
								constraintResult = new checkResultConstraint(){
									source = source,
									constraint = constraint,
									sourceComponent = sourceComponent,
									sourceIndex = sourceIndex,
									originalGameObj = gameObj
								}
							};
							group.results.Add(newResult);
						}
						sourceIndex++;
						// If not exist, then try and find gameObj or component with same name (multiple fixes is an array)
					}
				}
			}

			group.isChecking = false;
			group.hasResults = results.Count>0;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
		}

		private void script_constraints_displayResults(checkGroup group){
			var results = group.results; bool resultsShown = false;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			List<GameObject> uniqueGameObjects = new List<GameObject>();
			foreach(Component component in mainTargetObject.GetComponentsInChildren<Component>(true)){
				if(!uniqueGameObjects.Contains(component.gameObject)) uniqueGameObjects.Add(component.gameObject);
			}
			EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(0,0,0,0) });
			foreach(checkResult result in results){
				checkResultConstraint constraintResult = result.constraintResult;
				UnityEngine.Animations.IConstraint constraint = constraintResult.constraint;
				//UnityEngine.Animations.ConstraintSource source = constraintResult.source;
				Component sourceComponent = constraintResult.sourceComponent;
				var originalGameObj = (UnityObject)constraintResult?.originalGameObj;
				var currentGameObj = (UnityObject)sourceComponent?.gameObject;
				var originalSource = constraintResult.source;
				var currentSource = constraint.GetSource(constraintResult.sourceIndex);
				var newTransform = currentSource.sourceTransform;
				if(!toggleShowAllResults && !result.hasWarning) continue;
				if(!toggleShowAllResults && !(result.hasWarning || originalSource.sourceTransform!=currentSource.sourceTransform)) continue;
				EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.box){ margin=new RectOffset(0,0,10,10), padding=new RectOffset(0,0,0,0) });
				// Objects
				var of1 = EditorGUILayout.ObjectField("Constraint Source",(Component)constraint,typeof(Component),true);
				if((Component)of1!=(Component)constraint) ShowNotification(new GUIContent("Not Editable"));
				if(currentGameObj!=null && originalSource.Equals(currentSource) && originalSource.sourceTransform==currentSource.sourceTransform){
					newTransform = (Transform)EditorGUILayout.ObjectField("Transform Object",currentSource.sourceTransform,currentSource.sourceTransform.GetType(),true);
				}
				else {
					GUIAddLabel("(Edited) Transform Object:");
					EditorGUILayout.ObjectField("Checked Object",originalSource.sourceTransform,originalSource.sourceTransform.GetType(),true);
					newTransform = (Transform)EditorGUILayout.ObjectField("Current Object",currentSource.sourceTransform,currentSource.sourceTransform.GetType(),true);
				}
				if(newTransform!=currentSource.sourceTransform){
					if(group.isChecking || !toggleAllowEditing){
						ShowNotification(new GUIContent("Editing is currently disabled"));
						newTransform = currentSource.sourceTransform;
					}
				}
				// Edit & Actions
				if(!group.isChecking && toggleAllowEditing && newTransform!=currentSource.sourceTransform){
					Debug.Log("Edited Transform Object. From: "+currentSource.sourceTransform+" To: "+newTransform);
					constraint.SetSource(constraintResult.sourceIndex,new UnityEngine.Animations.ConstraintSource(){ sourceTransform=newTransform, weight=currentSource.weight });
					currentSource = constraint.GetSource(constraintResult.sourceIndex);
				}
				if(originalSource.sourceTransform!=currentSource.sourceTransform){
					EditorGUILayout.BeginHorizontal();
					var btnReset = GUILayout.Button(new GUIContent("Undo / Reset to checked object","From: "+currentSource.sourceTransform+" To: "+originalSource.sourceTransform));
					EditorGUILayout.EndHorizontal();
					if(btnReset && currentSource.sourceTransform!=originalSource.sourceTransform){
						btnReset = false;
						Debug.Log("Undo / Reset Transform Object. From: "+currentSource.sourceTransform+" To: "+originalSource.sourceTransform);
						constraint.SetSource(constraintResult.sourceIndex,new UnityEngine.Animations.ConstraintSource(){ sourceTransform=originalSource.sourceTransform, weight=currentSource.weight });
						currentSource = constraint.GetSource(constraintResult.sourceIndex);
					}
				}
				// Warnings
				result.warningMessages.Clear();
				if(currentSource.sourceTransform!=null && currentSource.sourceTransform is Component @comp && @comp.gameObject!=null && @comp.gameObject is UnityEngine.GameObject @object && !uniqueGameObjects.Contains(@object)){
					var warning = "Source Transform Object not within target object.";
					result.warningMessages.Add(null);
					GUIAddLabel("<color='#FFB0B0'><b>Warning:</b></color> <color='#F3E9A9'>"+warning+"</color>","",new GUIStyle(){ margin=new RectOffset(0,0,5,0) });
					var of = EditorGUILayout.ObjectField(new GUIContent("GameObject"),@object,typeof(UnityEngine.GameObject),true);
					if(of!=@object) ShowNotification(new GUIContent("Edit the \"Transform Object\" field above instead"));
				}
				result.hasWarning = result.warningMessages.Count>0;
				if(result.hasWarning && result.warningMessages.Exists(w=>w!=null)){
					EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,5,0), padding=new RectOffset(0,0,0,0) });
					foreach(string warning in result.warningMessages){
						if(warning!=null) GUIAddLabel("<color='#FFB0B0'><b>Warning:</b></color> <color='#F3E9A9'>"+warning+"</color>");
					}
					EditorGUILayout.EndVertical();
				}
				EditorGUILayout.EndVertical();
				resultsShown = true;
			}
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			if(group.isChecking) GUIAddLabel("Checking...");
			else if(results.Count==0) GUIAddLabel("No Results");
			else if(!resultsShown) GUIAddLabel("Everything seems OK.");
			EditorGUILayout.EndVertical();
		}

		// ##################################################################################

		private void script_vrcParams_runCheck(checkGroup group){
			var results = group.results = new List<checkResult>();
			group.hasResults = group.hasWarnings = false; group.isChecking = true;
			//if(debugLog){ scriptLog("Running Checks: "+group.name); }
			// TODO
			group.isChecking = false;
			group.hasResults = results.Count>0;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
		}

		private void script_vrcParams_displayResults(checkGroup group){
			var results = group.results; bool resultsShown = false;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(0,0,0,0) });
			GUIAddLabel("Not Yet Implemented");
			foreach(checkResult result in results){
				
				if(!toggleShowAllResults && !(result.hasWarning)) continue;
				resultsShown = true;
				EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.box){ margin=new RectOffset(0,0,7,7), padding=new RectOffset(0,0,0,0) });
				
				EditorGUILayout.EndVertical();
			}
			if(results.Count==0) GUIAddLabel("No Results");
			else if(!resultsShown) GUIAddLabel("Everything seems OK.");
			EditorGUILayout.EndVertical();
		}

		// ##################################################################################

		private void script_animations_runCheck(checkGroup group){
			var results = group.results = new List<checkResult>();
			group.hasResults = group.hasWarnings = false; group.isChecking = true;
			//if(debugLog){ scriptLog("Running Checks: "+group.name); }
			// TODO
			group.isChecking = false;
			group.hasResults = results.Count>0;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
		}

		private void script_animations_displayResults(checkGroup group){
			var results = group.results; bool resultsShown = false;
			group.hasWarnings = results.Count>0 && results.Exists(r=>r.hasWarning==true);
			EditorGUILayout.BeginVertical(new GUIStyle(){ margin=new RectOffset(0,0,0,0), padding=new RectOffset(0,0,0,0) });
			GUIAddLabel("Not Yet Implemented");
			foreach(checkResult result in results){
				
				if(!toggleShowAllResults && !(result.hasWarning)) continue;
				resultsShown = true;
				EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.box){ margin=new RectOffset(0,0,7,7), padding=new RectOffset(0,0,0,0) });
				
				EditorGUILayout.EndVertical();
			}
			if(results.Count==0) GUIAddLabel("No Results");
			else if(!resultsShown) GUIAddLabel("Everything seems OK.");
			EditorGUILayout.EndVertical();
		}

	}
}

#endif
#endif
#endif
