using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;
using UnityEngine;
using UnityInjector.Attributes;
using CM3D2.ExternalSaveData.Managed;

namespace CM3D2.AddModsSlider.Plugin
{
    [PluginFilter("CM3D2x64"), PluginFilter("CM3D2x86"), PluginFilter("CM3D2VRx64")]
    [PluginName("CM3D2 AddModsSlider"), PluginVersion("0.1.2.17")]
    public class AddModsSlider : UnityInjector.PluginBase
    {

        #region Constants

        public const string PluginName = "AddModsSlider";
        public const string Version    = "0.1.2.17";
        
        private readonly string LogLabel = AddModsSlider.PluginName + " : ";
        
        private readonly float TimePerInit = 1.00f;

        private readonly int UIRootWidth       = 1920; // GemaObject.Find("UI Root").GetComponent<UIRoot>().manualWidth;
        private readonly int UIRootHeight      = 1080; // GemaObject.Find("UI Root").GetComponent<UIRoot>().manualHeight;
        private readonly int ScrollViewWidth   = 550; 
        private readonly int ScrollViewHeight  = 860;

        #endregion



        #region Variables

        private int   sceneLevel;
        private bool  xmlLoad        = false;
        private bool  visible        = false;
        private bool  bInitCompleted = false;

        private ModsParam mp;
        private Dictionary<string, Dictionary<string, float>> undoValue = new Dictionary<string, Dictionary<string, float>>();

        private Maid maid;
        private GameObject goAMSPanel;
        private GameObject goScrollView;
        private GameObject goScrollViewTable;
        private UICamera     uiCamara;
        private UIPanel      uiAMSPanel;
        private UIPanel      uiScrollPanel;
        private UIScrollView uiScrollView;
        private UIScrollBar  uiScrollBar;
        private UITable      uiTable;
        private Font         font;
        private Dictionary<string, Transform> trModUnit = new Dictionary<string, Transform>();
        private Dictionary<string, Dictionary<string, UILabel>> uiValueLable = new Dictionary<string, Dictionary<string, UILabel>>();

        #endregion



        #region Nested classes

        private class ModsParam 
        {
            private readonly string LogLabel = AddModsSlider.PluginName + " : ";

            public readonly string DefMatchPattern = @"([-+]?[0-9]*\.?[0-9]+)";
            public readonly string XmlFileName = Directory.GetCurrentDirectory() + @"\UnityInjector\Config\ModsParam.xml";
            
            
            public string XmlFormat;
            public List<string> sKey = new List<string>();

            public Dictionary<string, bool>     bEnabled       = new Dictionary<string, bool>();
            public Dictionary<string, string>   sDescription   = new Dictionary<string, string>();
            public Dictionary<string, string>   sType          = new Dictionary<string, string>();
            public Dictionary<string, bool>     bOnWideSlider  = new Dictionary<string, bool>();
            public Dictionary<string, bool>     bVisible       = new Dictionary<string, bool>();

            public Dictionary<string, string[]> sPropName      = new Dictionary<string, string[]>();
            public Dictionary<string, Dictionary<string, float>>  fValue        = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVmin         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVmax         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVdef         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, string>> sVType        = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> sLabel        = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> sMatchPattern = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, bool>>   bVVisible     = new Dictionary<string, Dictionary<string, bool>>();

            public int KeyCount { get{return sKey.Count;} }
            public int ValCount(string key) { return sPropName[key].Length; }

            //--------

            public ModsParam() {}

            public bool Init()
            {
                if(!loadModsParamXML()) 
                {
                    Debug.LogError(LogLabel +"loadModsParamXML() failed.");
                    return false;
                }
                foreach(string key in sKey) CheckWS(key);
                
                return true;
            }

            public bool CheckWS(string key) 
            {
                return !bOnWideSlider[key] || (sKey.Contains("WIDESLIDER") && bEnabled["WIDESLIDER"]);
            }

            public bool IsToggle(string key) 
            {
                return (sType[key].Contains("toggle")) ? true : false;
            }

            public bool IsSlider(string key) 
            {
                return (sType[key].Contains("slider")) ? true : false;
            }

            //--------

            private bool loadModsParamXML()
            {
                if (!File.Exists(XmlFileName)) 
                {
                    Debug.LogError(LogLabel +"\"" + XmlFileName + "\" does not exist.");
                    return false;
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(XmlFileName);

                 XmlNode mods = doc.DocumentElement;
                 XmlFormat = ((XmlElement)mods).GetAttribute("format");
                if (XmlFormat != "1.2" && XmlFormat != "1.21")
                {
                    Debug.LogError(LogLabel +""+ AddModsSlider.Version +" requires fomart=\"1.2\" or \"1.21\" of ModsParam.xml.");
                    return false;
                }
                
                XmlNodeList modNodeS = mods.SelectNodes("/mods/mod");
                if (!(modNodeS.Count > 0)) 
                {
                    Debug.LogError(LogLabel +" \"" + XmlFileName + "\" has no <mod>elements.");
                    return false;
                }

                sKey.Clear();

                foreach(XmlNode modNode in modNodeS)
                {    
                    // mod属性
                    string key = ((XmlElement)modNode).GetAttribute("id");
                    if(key != "" && !sKey.Contains(key)) sKey.Add(key);
                    else continue;

                    bool b = false;
                    bEnabled[key]       = false;
                    sDescription[key]   = ((XmlElement)modNode).GetAttribute("description");
                    bOnWideSlider[key]  = (Boolean.TryParse(((XmlElement)modNode).GetAttribute("on_wideslider"),  out b)) ? b : false;
                    bVisible[key]       = (Boolean.TryParse(((XmlElement)modNode).GetAttribute("visible"),        out b)) ? b : true;
                    
                    sType[key] = ((XmlElement)modNode).GetAttribute("type");
                    switch (sType[key])
                    {
                        case "toggle": break;
                        case "toggle,slider": break;
                        default: sType[key] = "slider"; break;
                    }

                    if (!IsSlider(key)) continue;

                    XmlNodeList valueNodeS = ((XmlElement)modNode).GetElementsByTagName("value");
                    if (!(valueNodeS.Count > 0)) continue;
                    
                    sPropName[key]     = new string[valueNodeS.Count];
                    fValue[key]        = new Dictionary<string, float>();
                    fVmin[key]         = new Dictionary<string, float>();
                    fVmax[key]         = new Dictionary<string, float>();
                    fVdef[key]         = new Dictionary<string, float>();
                    sVType[key]        = new Dictionary<string, string>();
                    sLabel[key]        = new Dictionary<string, string>();
                    sMatchPattern[key] = new Dictionary<string, string>();
                    bVVisible[key]     = new Dictionary<string, bool>();
                    
                    // value属性
                    int j = 0;
                    foreach (XmlNode valueNode in valueNodeS)
                    {
                        float x = 0f;
                        
                        string prop = ((XmlElement)valueNode).GetAttribute("prop_name");
                        if (prop != "" && Array.IndexOf(sPropName[key], prop) < 0 )
                        {
                            sPropName[key][j] = prop;
                        }
                        else
                        {    
                            sKey.Remove(key);
                            break;
                        }
                        
                        sVType[key][prop] = ((XmlElement)valueNode).GetAttribute("type");
                        switch (sVType[key][prop])
                        {
                            case "num":   break;
                            case "scale": break;
                            case "int" :  break;
                            default : sVType[key][prop] = "num"; break;
                        }

                        fVmin[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("min"),     out x) ? x : 0f;
                        fVmax[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("max"),     out x) ? x : 0f;
                        fVdef[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("default"), out x) ? x : Single.NaN;
                        if (Single.IsNaN(fVdef[key][prop]))
                        {
							switch (sVType[key][prop])
                        	{
	                            case "num":   fVdef[key][prop] = 0f; break;
	                            case "scale": fVdef[key][prop] = 1f; break;
    	                        case "int" :  fVdef[key][prop] = 0f; break;
        	                    default :  fVdef[key][prop] = 0f; break;
                        	}
                        }

                        fValue[key][prop] = fVdef[key][prop];

                        sLabel[key][prop]        = ((XmlElement)valueNode).GetAttribute("label");
                        sMatchPattern[key][prop] = ((XmlElement)valueNode).GetAttribute("match_pattern");
                        bVVisible[key][prop]     = (Boolean.TryParse(((XmlElement)valueNode).GetAttribute("visible"), out b)) ? b : true;

                        j++;
                    }
                    if (j == 0) sKey.Remove(key);
                }
                
                return true;
            }
        }

        #endregion



        #region MonoBehaviour methods

        public void OnLevelWasLoaded(int level)
        {
	        if (level == 9) 
	        {
				font = GameObject.Find("SystemUI Root").GetComponentsInChildren<UILabel>()[0].trueTypeFont;
			}

            if (level != sceneLevel && sceneLevel == 5) finalize();

            if (level == 5)
            {
	            mp = new ModsParam();
                if (xmlLoad = mp.Init())  StartCoroutine( initCoroutine() );
            }
            
            sceneLevel = level;
        }

        public void Update()
        {
            if (sceneLevel == 5 && bInitCompleted)
            {
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    goAMSPanel.SetActive(visible = !visible);
                    //WriteTrans("UI Root");
                }
                
        	}
        }

        #endregion



        #region Callbacks

        public void OnClickHeaderButton()
        {
          try{
            string key = getTag(UIButton.current, 1);
			bool b = false;

			if (mp.IsToggle(key))
			{
				b = !mp.bEnabled[key];
				mp.bEnabled[key] = b;
	            setExSaveData(key);

	            notifyMaidVoicePitchOnChange();

	            // WIDESLIDER有効化/無効化に合わせて、依存項目UIを表示/非表示
	            if (key == "WIDESLIDER")  toggleActiveOnWideSlider();
	        }
			
			if (mp.IsSlider(key))
			{
				if (!mp.IsToggle(key)) b = !(UIButton.current.defaultColor.a == 1f);
				setSliderVisible(key, b);
			}

            setButtonColor(UIButton.current, b);

          } catch(Exception ex) { Debug.Log(LogLabel +"OnClickToggleHeader() "+ ex); return; }
		}

        public void OnClickUndoAll()
        {
          try{
			foreach (string key in mp.sKey)
			{
				if (mp.IsToggle(key))
				{
					mp.bEnabled[key] = (undoValue[key]["enable"] == 1f);
		            setExSaveData(key);
		            notifyMaidVoicePitchOnChange();
				    setButtonColor(key, mp.bEnabled[key]);
				}

				if (mp.IsSlider(key))
				{
					undoSliderValue(key);
					
					if (mp.IsToggle(key))
					{
						setSliderVisible(key, mp.bEnabled[key]);
					}
				}
			}
          } catch(Exception ex) { Debug.Log(LogLabel +"OnClickUndoAll() "+ ex); return; }
		}

        public void OnClickUndoButton()
        {
			undoSliderValue(getTag(UIButton.current, 1));
		}

        public void OnClickResetAll()
        {
          try{
			foreach (string key in mp.sKey)
			{
				if (mp.IsToggle(key))
				{
					mp.bEnabled[key] = false;
		            setExSaveData(key);
		            notifyMaidVoicePitchOnChange();
				    setButtonColor(key, mp.bEnabled[key]);
				}

				if (mp.IsSlider(key))
				{
					resetSliderValue(key);
					
					if (mp.IsToggle(key))
					{
						setSliderVisible(key, mp.bEnabled[key]);
					}
				}
			}
          } catch(Exception ex) { Debug.Log(LogLabel +"OnClickResetAll() "+ ex); return; }
		}

        public void OnClickResetButton()
        {
			resetSliderValue(getTag(UIButton.current, 1));
		}

        public void OnChangeSlider()
        {
          try{
            string key   = getTag(UIProgressBar.current, 1);
            string prop  = getTag(UIProgressBar.current, 2);
            float  value = codecSliderValue(key, prop, UIProgressBar.current.value);
            string vType = mp.sVType[key][prop];

            uiValueLable[key][prop].text = value.ToString("F2");
            mp.fValue[key][prop] = value;

            setExSaveData(key, prop);

            notifyMaidVoicePitchOnChange();

 			//Debug.Log(key +":"+ prop +":"+ value);
         } catch(Exception ex) { Debug.Log(LogLabel +"OnChangeSlider() "+ ex); return; }
        }

        public void OnSubmitSliderValueInput()
		{
          try{
			string key  = getTag(UIInput.current, 1);
            string prop = getTag(UIInput.current, 2);
            UISlider slider = null;

            foreach (Transform t in  UIInput.current.transform.parent.parent)
            {
				if(getTag(t, 0) == "Slider") slider = t.GetComponent<UISlider>();
			}

            float value;
	        if ( Single.TryParse(UIInput.current.value, out value) ) 
	        {
				mp.fValue[key][prop] = value;
				slider.value = codecSliderValue(key, prop);
				UIInput.current.value = codecSliderValue(key, prop, slider.value).ToString("F2");
			}
          } catch(Exception ex) { Debug.Log(LogLabel +"OnSubmitSliderValueInput() "+ ex); return; }
		}

        #endregion



        #region Private methods

        private IEnumerator initCoroutine()
        {
            while ( !(bInitCompleted = initialize()) ) yield return new WaitForSeconds(TimePerInit);
            Debug.Log(LogLabel +"Initialization complete.");
        }

        private bool initialize()
        {
          try{

            maid = GameMain.Instance.CharacterMgr.GetMaid(0);
			if (maid == null) return false;

			UIAtlas uiAtlasSceneEdit = FindAtlas("AtlasSceneEdit");
			UIAtlas uiAtlasDialog    = FindAtlas("SystemDialog");

            GameObject goUIRoot = GameObject.Find("UI Root");
            GameObject cameraObject    = GameObject.Find("/UI Root/Camera");
            Camera     cameraComponent = cameraObject.GetComponent<Camera>();
            uiCamara = cameraObject.GetComponent<UICamera>();

			#region createSlider

			// スライダー作成
			GameObject goTestSliderUnit = new GameObject("TestSliderUnit");
			SetChild(goUIRoot, goTestSliderUnit);
			{
				UISprite uiTestSliderUnitFrame = goTestSliderUnit.AddComponent<UISprite>();
				uiTestSliderUnitFrame.atlas      = uiAtlasSceneEdit;
				uiTestSliderUnitFrame.spriteName = "cm3d2_edit_slidertitleframe";
				uiTestSliderUnitFrame.type       = UIBasicSprite.Type.Sliced;
				uiTestSliderUnitFrame.SetDimensions(500, 50);

				// スライダー作成
				UISlider uiTestSlider = NGUITools.AddChild<UISlider>(goTestSliderUnit);
				UISprite uiTestSliderRail = uiTestSlider.gameObject.AddComponent<UISprite>();
				uiTestSliderRail.name       = "Slider";
				uiTestSliderRail.atlas      = uiAtlasSceneEdit;
				uiTestSliderRail.spriteName = "cm3d2_edit_slideberrail";
				uiTestSliderRail.type       = UIBasicSprite.Type.Sliced;
				uiTestSliderRail.SetDimensions(250, 5);

				UIWidget uiTestSliderBar = NGUITools.AddChild<UIWidget>(uiTestSlider.gameObject);
				uiTestSliderBar.name  = "DummyBar";
				uiTestSliderBar.width = uiTestSliderRail.width;

				UISprite uiTestSliderThumb = NGUITools.AddChild<UISprite>(uiTestSlider.gameObject);
				uiTestSliderThumb.name       = "Thumb";
				uiTestSliderThumb.depth      = uiTestSliderRail.depth + 1;
				uiTestSliderThumb.atlas      = uiAtlasSceneEdit;
				uiTestSliderThumb.spriteName = "cm3d2_edit_slidercursor";
				uiTestSliderThumb.type       = UIBasicSprite.Type.Sliced;
				uiTestSliderThumb.SetDimensions(25, 25);
				uiTestSliderThumb.gameObject.AddComponent<BoxCollider>();

				uiTestSlider.backgroundWidget = uiTestSliderRail;
				uiTestSlider.foregroundWidget = uiTestSliderBar;
				uiTestSlider.thumb            = uiTestSliderThumb.gameObject.transform;
				uiTestSlider.value            = 0.5f;
				uiTestSlider.gameObject.AddComponent<BoxCollider>();
				uiTestSlider.transform.localPosition = new Vector3(100f, 0f, 0f);

				NGUITools.UpdateWidgetCollider(uiTestSlider.gameObject);
				NGUITools.UpdateWidgetCollider(uiTestSliderThumb.gameObject);

				// スライダーラベル作成
				UILabel uiTestSliderLabel = NGUITools.AddChild<UILabel>(goTestSliderUnit);
				uiTestSliderLabel.name           = "Label";
				uiTestSliderLabel.trueTypeFont   = font;
				uiTestSliderLabel.fontSize       = 20;
				uiTestSliderLabel.text           = "テストスライダー";
				uiTestSliderLabel.width          = 110;
				uiTestSliderLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

				uiTestSliderLabel.transform.localPosition = new Vector3(-190f, 0f, 0f);
				
				// 値ラベル・インプット作成
				UISprite uiTestSliderValueBase = NGUITools.AddChild<UISprite>(goTestSliderUnit);
				uiTestSliderValueBase.name       = "ValueBase";
				uiTestSliderValueBase.atlas      = uiAtlasSceneEdit;
				uiTestSliderValueBase.spriteName = "cm3d2_edit_slidernumberframe";
				uiTestSliderValueBase.type       = UIBasicSprite.Type.Sliced;
				uiTestSliderValueBase.SetDimensions(80, 35);
				uiTestSliderValueBase.transform.localPosition = new Vector3(-90f, 0f, 0f);

				UILabel uiTestSliderValueLabel = NGUITools.AddChild<UILabel>(uiTestSliderValueBase.gameObject);
				uiTestSliderValueLabel.name         = "Value";
				uiTestSliderValueLabel.depth        = uiTestSliderValueBase.depth + 1;
				uiTestSliderValueLabel.width        = uiTestSliderValueBase.width;
				uiTestSliderValueLabel.trueTypeFont = font;
				uiTestSliderValueLabel.fontSize     = 20;
				uiTestSliderValueLabel.text         = "0.00";
				uiTestSliderValueLabel.color        = Color.black;

	            UIInput uiTestSliderValueInput = uiTestSliderValueLabel.gameObject.AddComponent<UIInput>();
	            uiTestSliderValueInput.label           = uiTestSliderValueLabel;
	            uiTestSliderValueInput.onReturnKey     = UIInput.OnReturnKey.Submit;
	            uiTestSliderValueInput.validation      = UIInput.Validation.Float;
				uiTestSliderValueInput.activeTextColor = Color.black;
				uiTestSliderValueInput.caretColor      = new Color(0.1f, 0.1f, 0.3f, 1f);
				uiTestSliderValueInput.selectionColor  = new Color(0.3f, 0.3f, 0.6f, 0.8f);
				//EventDelegate.Add(uiTestSliderValueInput.onSubmit, new EventDelegate.Callback(this.OnSubmitSliderValueInput));
				
				uiTestSliderValueInput.gameObject.AddComponent<BoxCollider>();
				NGUITools.UpdateWidgetCollider(uiTestSliderValueInput.gameObject);
			}
			goTestSliderUnit.SetActive(false);

			#endregion
			

            // ボタンはgoProfileTabをコピー
            GameObject goProfileTabCopy = UnityEngine.Object.Instantiate( FindChild(goUIRoot.transform.Find("ProfilePanel").Find("Comment").gameObject, "ProfileTab") ) as GameObject;
            EventDelegate.Remove(goProfileTabCopy.GetComponent<UIButton>().onClick, new EventDelegate.Callback(ProfileMgr.Instance.ChangeCommentTab));
			goProfileTabCopy.SetActive(false);


			#region createPanel
			
			// ModsSliderPanel作成
            Vector3 originAMSPanel = new Vector3(UIRootWidth / 2f - 15f - ScrollViewWidth / 2f - 50f, 40f, 0f);
            int systemUnitHeight = 30;

			// 親Panel
            uiAMSPanel = NGUITools.AddChild<UIPanel>(goUIRoot);
            uiAMSPanel.name = "ModsSliderPanel";
            uiAMSPanel.transform.localPosition = originAMSPanel;
            goAMSPanel = uiAMSPanel.gameObject;

			// 背景
            UISprite uiBGSprite = NGUITools.AddChild<UISprite>(goAMSPanel);
            uiBGSprite.name       = "BG";
			uiBGSprite.atlas      = uiAtlasSceneEdit;
			uiBGSprite.spriteName = "cm3d2_edit_window_l";
			uiBGSprite.type       = UIBasicSprite.Type.Sliced;
			uiBGSprite.SetDimensions(ScrollViewWidth, ScrollViewHeight);

			// ScrollViewPanel
            uiScrollPanel = NGUITools.AddChild<UIPanel>(goAMSPanel);
            uiScrollPanel.name         = "ScrollView";
            uiScrollPanel.sortingOrder = uiAMSPanel.sortingOrder + 1;
            uiScrollPanel.clipping     = UIDrawCall.Clipping.SoftClip;
            uiScrollPanel.SetRect(0f, 0f, uiBGSprite.width, uiBGSprite.height - 110 - systemUnitHeight);
			uiScrollPanel.transform.localPosition = new Vector3(-25f, - systemUnitHeight, 0f);
            goScrollView = uiScrollPanel.gameObject;

            uiScrollView = goScrollView.AddComponent<UIScrollView>();
            uiScrollView.contentPivot = UIWidget.Pivot.Center;
            uiScrollView.movement = UIScrollView.Movement.Vertical;
            uiScrollView.scrollWheelFactor = 1.5f;

            uiBGSprite.gameObject.AddComponent<UIDragScrollView>().scrollView = uiScrollView;
            uiBGSprite.gameObject.AddComponent<BoxCollider>();
            NGUITools.UpdateWidgetCollider(uiBGSprite.gameObject);
            
            // ScrollBar
			uiScrollBar = NGUITools.AddChild<UIScrollBar>(goAMSPanel);
			uiScrollBar.value = 0f;
			uiScrollBar.gameObject.AddComponent<BoxCollider>();
			uiScrollBar.transform.localPosition = new Vector3(uiBGSprite.width / 2f-10, 0f, 0f);
			uiScrollBar.transform.localRotation *= Quaternion.Euler(0f, 0f, -90f);

			UIWidget uiScrollBarFore = NGUITools.AddChild<UIWidget>(uiScrollBar.gameObject);
			uiScrollBarFore.name   = "DummyFore";
			uiScrollBarFore.height = 15;
			uiScrollBarFore.width  = uiBGSprite.height;

			UISprite uiScrollBarThumb = NGUITools.AddChild<UISprite>(uiScrollBar.gameObject);
			uiScrollBarThumb.name       = "Thumb";
			uiScrollBarThumb.depth      = uiBGSprite.depth + 1;
			uiScrollBarThumb.atlas      = uiAtlasSceneEdit;
			uiScrollBarThumb.spriteName = "cm3d2_edit_slidercursor";
			uiScrollBarThumb.type       = UIBasicSprite.Type.Sliced;
			uiScrollBarThumb.SetDimensions(15, 15);
			uiScrollBarThumb.gameObject.AddComponent<BoxCollider>();

			uiScrollBar.foregroundWidget = uiScrollBarFore;
			uiScrollBar.thumb            = uiScrollBarThumb.transform;

			NGUITools.UpdateWidgetCollider(uiScrollBarFore.gameObject);
			NGUITools.UpdateWidgetCollider(uiScrollBarThumb.gameObject);
            uiScrollView.verticalScrollBar = uiScrollBar;

			// ScrollView内のTable
            uiTable = NGUITools.AddChild<UITable>(goScrollView);
            uiTable.pivot           = UIWidget.Pivot.Center;
            uiTable.columns         = 1;
            uiTable.padding         = new Vector2(25f, 10f);
            uiTable.hideInactive    = true;
            uiTable.keepWithinPanel = true;
            uiTable.sorting         = UITable.Sorting.Custom;
            uiTable.onCustomSort    = (Comparison<Transform>)this.sortGridByXMLOrder;
            //uiTable.onReposition    = this.OnRepositionTable;
            goScrollViewTable = uiTable.gameObject;
            //uiScrollView.centerOnChild = goScrollViewTable.AddComponent<UICenterOnChild>();

            // ドラッグ用タブ（タイトル部分）
			UISprite uiSpriteTitleTab = NGUITools.AddChild<UISprite>(goAMSPanel);
            uiSpriteTitleTab.name       = "TitleTab";
            uiSpriteTitleTab.depth      = uiBGSprite.depth - 1;
			uiSpriteTitleTab.atlas      = uiAtlasDialog;
			uiSpriteTitleTab.spriteName = "cm3d2_dialog_frame";
			uiSpriteTitleTab.type       = UIBasicSprite.Type.Sliced;
			uiSpriteTitleTab.SetDimensions(300, 80);
            uiSpriteTitleTab.autoResizeBoxCollider = true;
            uiSpriteTitleTab.gameObject.AddComponent<UIDragObject>().target = goAMSPanel.transform;
            uiSpriteTitleTab.gameObject.AddComponent<BoxCollider>().isTrigger = true;
            NGUITools.UpdateWidgetCollider(uiSpriteTitleTab.gameObject);
            uiSpriteTitleTab.transform.localPosition = new Vector3(uiBGSprite.width / 2f + 5f, (uiBGSprite.height - uiSpriteTitleTab.width) / 2f, 0f);
			uiSpriteTitleTab.transform.localRotation *= Quaternion.Euler(0f, 0f, -90f);
            
            UILabel uiLabelTitleTab = uiSpriteTitleTab.gameObject.AddComponent<UILabel>();
			uiLabelTitleTab.depth        = uiSpriteTitleTab.depth + 1;
			uiLabelTitleTab.width        = uiSpriteTitleTab.width;
			uiLabelTitleTab.color        = Color.white;
			uiLabelTitleTab.trueTypeFont = font;
			uiLabelTitleTab.fontSize     = 18;
			uiLabelTitleTab.text         = "Mods Slider " + AddModsSlider.Version;

			int conWidth = (int)(uiBGSprite.width - uiTable.padding.x * 2);
			int baseTop  = (int)(uiBGSprite.height / 2f - 50);

			GameObject goSystemUnit = NGUITools.AddChild(goAMSPanel);
			goSystemUnit.name = ("System:Undo");

			// Undoボタン
            GameObject goUndoAll = SetCloneChild(goSystemUnit, goProfileTabCopy, "UndoAll");
            goUndoAll.transform.localPosition = new Vector3(-conWidth * 0.25f - 6, baseTop - systemUnitHeight / 2f, 0f);
			goUndoAll.AddComponent<UIDragScrollView>().scrollView = uiScrollView;

			UISprite uiSpriteUndoAll = goUndoAll.GetComponent<UISprite>();
            uiSpriteUndoAll.SetDimensions((int)(conWidth * 0.5f) - 2, systemUnitHeight); 

			UILabel uiLabelUndoAll = FindChild(goUndoAll,"Name").GetComponent<UILabel>();
			uiLabelUndoAll.width           = uiSpriteUndoAll.width  - 10;
			uiLabelUndoAll.fontSize        = 22;
			uiLabelUndoAll.spacingX        = 0;
			uiLabelUndoAll.supportEncoding = true; 
			uiLabelUndoAll.text            = "[111111]UndoAll";

			UIButton uiButtonUndoAll = goUndoAll.GetComponent<UIButton>();
			uiButtonUndoAll.defaultColor = new Color(1f, 1f, 1f, 0.8f);
            EventDelegate.Set(uiButtonUndoAll.onClick, new EventDelegate.Callback(this.OnClickUndoAll));

			FindChild(goUndoAll,"SelectCursor").GetComponent<UISprite>().SetDimensions(16,16);
			FindChild(goUndoAll,"SelectCursor").SetActive(false);
	        NGUITools.UpdateWidgetCollider(goUndoAll);
	        goUndoAll.SetActive(true);

			// Resetボタン
	        GameObject goResetAll = SetCloneChild(goSystemUnit, goUndoAll, "ResetAll");
	        goResetAll.transform.localPosition = new Vector3(conWidth * 0.25f - 4, baseTop - systemUnitHeight / 2f, 0f);

			UILabel uiLabelResetAll = FindChild(goResetAll,"Name").GetComponent<UILabel>();
			uiLabelResetAll.text = "[111111]ResetAll";

			UIButton uiButtonResetAll = goResetAll.GetComponent<UIButton>();
			uiButtonResetAll.defaultColor = new Color(1f, 1f, 1f, 0.8f);
            EventDelegate.Set(uiButtonResetAll.onClick, new EventDelegate.Callback(this.OnClickResetAll));

	        NGUITools.UpdateWidgetCollider(goResetAll);
	        goResetAll.SetActive(true);

            #endregion



            // 拡張セーブデータ読込
			Debug.Log(LogLabel +"Loading ExternalSaveData...");
			Debug.Log("----------------ExternalSaveData----------------");
            getExSaveData();
			Debug.Log("------------------------------------------------");



			#region addTableContents

            // ModsParamの設定に従ってボタン・スライダー追加
            for (int i = 0; i < mp.KeyCount; i++)
            {
                string key = mp.sKey[i];

                if (!mp.bVisible[key]) continue;

                uiValueLable[key]  = new Dictionary<string, UILabel>();
                string modeDesc = mp.sDescription[key] + " (" + key + ")";

				// ModUnit：modタグ単位のまとめオブジェクト ScrollViewGridの子
				GameObject goModUnit = NGUITools.AddChild(goScrollViewTable);
				goModUnit.name = ("Unit:" + key);
				trModUnit[key] = goModUnit.transform;

				// プロフィールタブ複製・追加
				GameObject goHeaderButton = SetCloneChild(goModUnit, goProfileTabCopy, "Header:"+ key);
				goHeaderButton.SetActive(true);
                goHeaderButton.AddComponent<UIDragScrollView>().scrollView = uiScrollView;
				UIButton uiHeaderButton = goHeaderButton.GetComponent<UIButton>();
                EventDelegate.Set(uiHeaderButton.onClick, new EventDelegate.Callback(this.OnClickHeaderButton));
                setButtonColor(uiHeaderButton,  mp.IsToggle(key) ?  mp.bEnabled[key] : false );

				// 白地Sprite
				UISprite uiSpriteHeaderButton = goHeaderButton.GetComponent<UISprite>();
				uiSpriteHeaderButton.type = UIBasicSprite.Type.Sliced;
                uiSpriteHeaderButton.SetDimensions(conWidth, 40); 

                UILabel uiLabelHeader = FindChild(goHeaderButton, "Name").GetComponent<UILabel>();
                uiLabelHeader.width          = uiSpriteHeaderButton.width - 20; 
                uiLabelHeader.height         = 30; 
				uiLabelHeader.trueTypeFont   = font;
				uiLabelHeader.fontSize       = 22;
				uiLabelHeader.spacingX       = 0;
				uiLabelHeader.multiLine      = false;
				uiLabelHeader.overflowMethod = UILabel.Overflow.ClampContent;
				uiLabelHeader.supportEncoding= true; 
				uiLabelHeader.text           = "[000000]"+ modeDesc +"[-]";
                uiLabelHeader.gameObject.AddComponent<UIDragScrollView>().scrollView = uiScrollView;

                // 金枠Sprite
				UISprite uiSpriteHeaderCursor =  FindChild(goHeaderButton,"SelectCursor").GetComponent<UISprite>();
				uiSpriteHeaderCursor.gameObject.SetActive( mp.IsToggle(key) ?  mp.bEnabled[key] : false );

                NGUITools.UpdateWidgetCollider(goHeaderButton);
                
                // スライダーならUndo/Resetボタンとスライダー追加
                if (mp.IsSlider(key))
				{
	                uiSpriteHeaderButton.SetDimensions((int)(conWidth*0.8f), 40); 
	                uiLabelHeader.width = uiSpriteHeaderButton.width - 20; 
		            uiHeaderButton.transform.localPosition = new Vector3(-conWidth*0.1f, 0f, 0f);

					// Undoボタン
	                GameObject goUndo = SetCloneChild(goModUnit, goProfileTabCopy, "Undo:" + key);
		            goUndo.transform.localPosition = new Vector3(conWidth*0.4f+2, 10.5f, 0f);
					goUndo.AddComponent<UIDragScrollView>().scrollView = uiScrollView;

					UISprite uiSpriteUndo = goUndo.GetComponent<UISprite>();
		            uiSpriteUndo.SetDimensions((int)(conWidth*0.2f)-2, 19); 

					UILabel uiLabelUndo = FindChild(goUndo,"Name").GetComponent<UILabel>();
					uiLabelUndo.width           = uiSpriteUndo.width  - 10;
					uiLabelUndo.fontSize        = 14;
					uiLabelUndo.spacingX        = 0;
					uiLabelUndo.supportEncoding = true; 
					uiLabelUndo.text            = "[111111]Undo";
					
					UIButton uiButtonUndo = goUndo.GetComponent<UIButton>();
            		uiButtonUndo.defaultColor = new Color(1f, 1f, 1f, 0.8f);
					
                    EventDelegate.Set(uiButtonUndo.onClick, new EventDelegate.Callback(this.OnClickUndoButton));
					FindChild(goUndo,"SelectCursor").GetComponent<UISprite>().SetDimensions(16,16);
					FindChild(goUndo,"SelectCursor").SetActive(false);
	                NGUITools.UpdateWidgetCollider(goUndo);
	                goUndo.SetActive(true);

					// Resetボタン
	                GameObject goReset = SetCloneChild(goModUnit, goProfileTabCopy, "Reset:" + key);
					goReset.AddComponent<UIDragScrollView>().scrollView = uiScrollView;
		            goReset.transform.localPosition = new Vector3(conWidth*0.4f+2, -10.5f, 0f);

					UISprite uiSpriteReset = goReset.GetComponent<UISprite>();
		            uiSpriteReset.SetDimensions((int)(conWidth*0.2f)-2, 19); 

					UILabel uiLabelReset = FindChild(goReset,"Name").GetComponent<UILabel>();
					uiLabelReset.width           = uiSpriteReset.width - 10;
					uiLabelReset.fontSize        = 14;
					uiLabelReset.spacingX        = 0;
					uiLabelReset.supportEncoding = true; 
					uiLabelReset.text            = "[111111]Reset";
					
					UIButton uiButtonReset = goReset.GetComponent<UIButton>();
            		uiButtonReset.defaultColor = new Color(1f, 1f, 1f, 0.8f);
					
                    EventDelegate.Set(uiButtonReset.onClick, new EventDelegate.Callback(this.OnClickResetButton));
					FindChild(goReset,"SelectCursor").GetComponent<UISprite>().SetDimensions(16,16);
					FindChild(goReset,"SelectCursor").SetActive(false);
	                NGUITools.UpdateWidgetCollider(goReset);
	                goReset.SetActive(true);


                    for (int j=0; j<mp.ValCount(key); j++)
                    {
                        string prop = mp.sPropName[key][j];

                        if (!mp.bVVisible[key][prop]) continue;

                        float  value = mp.fValue[key][prop];
                        float  vmin  = mp.fVmin[key][prop];
                        float  vmax  = mp.fVmax[key][prop];
                        string label = mp.sLabel[key][prop];
                        string vType = mp.sVType[key][prop];

						// スライダーをModUnitに追加
						GameObject goSliderUnit = SetCloneChild(goModUnit, goTestSliderUnit, "SliderUnit");
			            goSliderUnit.transform.localPosition = new Vector3(0f, j * - 70f - uiSpriteHeaderButton.height-20f, 0f);
						goSliderUnit.AddComponent<UIDragScrollView>().scrollView = uiScrollView;

						// フレームサイズ
						goSliderUnit.GetComponent<UISprite>().SetDimensions(conWidth, 50);

						// スライダー設定
                        UISlider uiModSlider = FindChild(goSliderUnit,"Slider").GetComponent<UISlider>();
                        uiModSlider.name = "Slider:"+ key +":"+ prop;
                        uiModSlider.value = codecSliderValue(key, prop);
                        if (vType == "int") uiModSlider.numberOfSteps = (int)(vmax - vmin + 1);
                        EventDelegate.Add(uiModSlider.onChange, new EventDelegate.Callback(this.OnChangeSlider));

                        // スライダーラベル設定
                        FindChild(goSliderUnit,"Label").GetComponent<UILabel>().text = label;
						FindChild(goSliderUnit,"Label").AddComponent<UIDragScrollView>().scrollView = uiScrollView;

                        // スライダー値ラベル参照取得
                        GameObject goValueLabel = FindChild(goSliderUnit,"Value");
                        goValueLabel.name = "Value:"+ key +":"+ prop;
                        uiValueLable[key][prop] = goValueLabel.GetComponent<UILabel>();
						uiValueLable[key][prop].multiLine      = false;
                        EventDelegate.Set(goValueLabel.GetComponent<UIInput>().onSubmit, this.OnSubmitSliderValueInput);

                        // スライダー有効状態設定
                        //goSliderUnit.SetActive( !mp.IsToggle(key) || mp.bEnabled[key] && mp.CheckWS(key) );
                        goSliderUnit.SetActive(false);
                    }
                }

                // 金枠Sprite
				uiSpriteHeaderCursor.type = UIBasicSprite.Type.Sliced;
                uiSpriteHeaderCursor.SetDimensions(uiSpriteHeaderButton.width - 4, uiSpriteHeaderButton.height - 4); 
            }

			#endregion

            uiTable.Reposition();
            goAMSPanel.SetActive(false);

            //WriteTrans("UI Root");

          } catch(Exception ex) { Debug.Log(LogLabel +"initialize()"+ ex); return false;}

			return true;
		}
		
		private void finalize()
		{
			bInitCompleted = false;
			visible        = false;
			mp             = null;

			maid              = null;
			goAMSPanel        = null;
			goScrollView      = null;
			goScrollViewTable = null;
			
			uiValueLable.Clear();
		}
		
		//----

        public void toggleActiveOnWideSlider() { toggleActiveOnWideSlider(mp.bEnabled["WIDESLIDER"]); }
        public void toggleActiveOnWideSlider(bool b)
        {
          try{

            foreach (Transform t in goScrollViewTable.transform)
            {
				string goType = getTag(t, 0);
				string goKey  = getTag(t, 1);
				
				if (goType == "System") continue;
				
				if (mp.bOnWideSlider[goKey])
				{
					string s = (b ? "[000000]" : "[FF0000]WS必須 [-]") + mp.sDescription[goKey] + " (" + goKey + ")";
					t.GetComponentsInChildren<UILabel>()[0].text = s;
					
					UIButton uiButton = t.GetComponentsInChildren<UIButton>()[0];
					uiButton.isEnabled = b;
					if (!(b && mp.IsSlider(goKey))) setButtonColor(uiButton, b);

		            if (!b)
		            {
						foreach (Transform tc in t)
			            {
							string gocType = getTag(tc, 0);
							if (gocType == "SliderUnit" || gocType == "Spacer") tc.gameObject.SetActive(b);
						}
					}
				}
			}
            uiTable.repositionNow = true;

          } catch(Exception ex) { Debug.Log(LogLabel +"toggleActiveOnWideSlider() "+ ex); }
        }

        private void undoSliderValue(string key)
        {
          try{
			foreach (Transform tr in trModUnit[key])
			{
				if (tr.name == "SliderUnit")
				{
					UISlider slider = FindChildByTag(tr, "Slider").GetComponent<UISlider>();
					string prop = getTag(slider, 2);

					mp.fValue[key][prop] = undoValue[key][prop];
					slider.value = codecSliderValue(key, prop);
					//Debug.LogWarning(key + "#"+ getTag(slider, 2) +" = "+ undoValue[key][prop]);
				}
			}
          } catch(Exception ex) { Debug.Log(LogLabel +"undoSliderValue() "+ ex); }
        }
        
        private void resetSliderValue(string key)
        {
          try{
			foreach (Transform tr in trModUnit[key])
			{
				if (tr.name == "SliderUnit")
				{
					UISlider slider = FindChildByTag(tr, "Slider").GetComponent<UISlider>();
					string prop = getTag(slider, 2);
					
					mp.fValue[key][prop] = mp.fVdef[key][prop];
					slider.value = codecSliderValue(key, prop);

					//Debug.LogWarning(key + "#"+ getTag(slider, 2) +" = "+ mp.fVdef[key][prop]);
				}
			}
          } catch(Exception ex) { Debug.Log(LogLabel +"resetSliderValue() "+ ex); }
		}
        

        private int sortGridByXMLOrder(Transform t1, Transform t2)
        {
          try{
            string type1 = t1.name.Split(':')[0];
            string type2 = t2.name.Split(':')[0];
            string key1 = t1.name.Split(':')[1];
            string key2 = t2.name.Split(':')[1];
            int n = mp.sKey.IndexOf(key1);
            int m = mp.sKey.IndexOf(key2);
            
            //Debug.Log(t1.name +" comp "+ t2.name);

            Dictionary<string, int> order = new Dictionary<string, int>()
            	{ {"System", -1}, {"Unit", 0}, {"Panel", 1}, {"Header", 2}, {"Slider", 3}, {"Spacer", 4} };
            
            if (n == m) 
            {
                if (type1 == "Slider" && type2 == "Slider")
                {
                    int l = Array.IndexOf(mp.sPropName[key1], t1.name.Split(':')[2]);
                    int k = Array.IndexOf(mp.sPropName[key2], t2.name.Split(':')[2]);
                    
                    return l - k;
                }
                else return order[type1] - order[type2];
            }
            else return n - m;
          } catch(Exception ex) { Debug.Log(LogLabel +"sortGridByXMLOrder() "+ ex); return 0; }
        }

        private void setSliderVisible(string key, bool b)
        {
            foreach (Transform tc in trModUnit[key])
            {
				string type = getTag(tc, 0);
				if (type == "SliderUnit" || type == "Spacer") tc.gameObject.SetActive(b);
			}

			uiTable.repositionNow = true;
        }

        private void setButtonColor(string key, bool b)
        {
			setButtonColor(FindChild(trModUnit[key], "Header:"+ key).GetComponent<UIButton>(), b);
		}
        private void setButtonColor(UIButton button, bool b)
        {
            Color color = button.defaultColor;

			if ( mp.IsToggle(getTag(button, 1)) )
			{
				button.defaultColor = new Color(color.r, color.g, color.b,  b ? 1f : 0.5f);
				FindChild(button.gameObject, "SelectCursor").SetActive(b);
			}
			else
			{
				button.defaultColor = new Color(color.r, color.g, color.b,  b ? 1f : 0.75f);
			}
        }

		private void windowTweenFinished()
		{
			goScrollView.SetActive(true);
		}

		private string getTag(Component co, int n) { return getTag(co.gameObject, n); }
		private string getTag(GameObject go, int n)
		{
			return (go.name.Split(':') != null) ? go.name.Split(':')[n] : "";
		}

        private float codecSliderValue(string key, string prop)
        {
            float value = mp.fValue[key][prop];
            float vmin = mp.fVmin[key][prop];
            float vmax = mp.fVmax[key][prop];
            string vType = mp.sVType[key][prop];

            if (value < vmin) value = vmin;
            if (value > vmax) value = vmax;

            if (vType == "scale" && vmin < 1f)
            {
                if (vmin < 0f) vmin = 0f;
                if (value < 0f) value = 0f;

                return (value < 1f) ? (value - vmin)/(1f - vmin) * 0.5f : 0.5f + (value - 1f)/(vmax - 1f) * 0.5f;
            }
            else if (vType == "int") 
            {
                decimal dvalue = (decimal)value;
                decimal dvmin  = (decimal)vmin;
                decimal dvmax  = (decimal)vmax;
                
                return (float)Math.Round((dvalue - dvmin) / (dvmax - dvmin), 1, MidpointRounding.AwayFromZero);
            }
            else 
            {
                return (value - vmin) / (vmax - vmin);
            }
        }

        private float codecSliderValue(string key, string prop, float value)
        {
            float vmin = mp.fVmin[key][prop];
            float vmax = mp.fVmax[key][prop];
            string vType = mp.sVType[key][prop];

            if (value < 0f) value = 0f;
            if (value > 1f) value = 1f;

            if (vType == "scale" && vmin < 1f)
            {
                if (vmin < 0f) vmin = 0f;
                if (value < 0f) value = 0f;
                
                return (value < 0.5f) ?  vmin + (1f - vmin) * value * 2f :  1 + (vmax - 1f) * (value - 0.5f) * 2;
            }
            else if (vType == "int") 
            {
                decimal dvalue = (decimal)value;
                decimal dvmin  = (decimal)vmin;
                decimal dvmax  = (decimal)vmax;

                return (float)Math.Round(vmin + (vmax - vmin) * value, 0, MidpointRounding.AwayFromZero);
            }
            else 
            {
                return vmin + (vmax - vmin) * value;
            }
        }


        //--------
        
        private void notifyMaidVoicePitchOnChange()
        {
            this.gameObject.SendMessage("MaidVoicePitch_UpdateSliders");
        }

        private void getExSaveData()
        {
            string plugin = "CM3D2.MaidVoicePitch";
            for (int i=0; i<mp.KeyCount; i++)
            {
                string key = mp.sKey[i];
                undoValue[key] = new Dictionary<string, float>();
                
                if (mp.IsToggle(key))
                {
                    mp.bEnabled[key] = ExSaveData.GetBool(maid, plugin, key, false);
                    undoValue[key]["enable"] = (mp.bEnabled[key]) ? 1f : 0f;
                    Debug.Log( string.Format("{0,-32} = {1,-16}", key, mp.bEnabled[key]) );
                }

                if (mp.IsSlider(key))
                {
                    for (int j=0; j<mp.ValCount(key); j++)
                    {
                        string prop = mp.sPropName[key][j];
                        float f = ExSaveData.GetFloat(maid, plugin, prop, float.NaN);
                        mp.fValue[key][prop] =  float.IsNaN(f) ? mp.fVdef[key][prop] : f;
                        undoValue[key][prop] = mp.fValue[key][prop];

                        Debug.Log( string.Format("{0,-32} = {1:f}", prop, mp.fValue[key][prop]) );
                    }
                    if (!mp.IsToggle(key)) mp.bEnabled[key] = true;
                }
            }
        }

        private void setExSaveData()
        {
            for (int i=0; i<mp.KeyCount; i++) setExSaveData(mp.sKey[i]);
        }

        private void setExSaveData(string key)
        {
            string plugin = "CM3D2.MaidVoicePitch";

            if (mp.IsToggle(key))
            {
                ExSaveData.SetBool(maid, plugin, key, mp.bEnabled[key]);
            }
            
            if (mp.IsSlider(key))
            {
                for (int j=0; j<mp.ValCount(key); j++) setExSaveData(key, mp.sPropName[key][j]);
            }
        }

        private void setExSaveData(string key, string prop)
        {
            string plugin = "CM3D2.MaidVoicePitch";

            float value = (float)Math.Round(mp.fValue[key][prop], 3, MidpointRounding.AwayFromZero);

            ExSaveData.SetFloat(maid, plugin, prop, value);
        }

        #endregion



        #region Utility methods
        

        internal static Transform FindParent(Transform tr, string s) { return FindParent(tr.gameObject, s).transform; }
        internal static GameObject FindParent(GameObject go, string name)
        {
            if (go == null) return null;

            Transform _parent = go.transform.parent;
            while (_parent)
            {
                if (_parent.name == name) return _parent.gameObject;
                _parent = _parent.parent;
            }

            return null;
        }

        internal static Transform FindChild(Transform tr, string s) { return FindChild(tr.gameObject, s).transform; }
        internal static GameObject FindChild(GameObject go, string s)
        {
            if (go == null) return null;
            GameObject target = null;
            
            foreach (Transform tc in go.transform)
            {
                if (tc.gameObject.name == s) return tc.gameObject;
                target = FindChild(tc.gameObject, s);
                if (target) return target;
            } 
            
            return null;
        }
 
        internal static Transform FindChildByTag(Transform tr, string s) { return FindChildByTag(tr.gameObject, s).transform; }
        internal static GameObject FindChildByTag(GameObject go, string s)
        {
            if (go == null) return null;
            GameObject target = null;
            
            foreach (Transform tc in go.transform)
            {
                if (tc.gameObject.name.Contains(s)) return tc.gameObject;
                target = FindChild(tc.gameObject, s);
                if (target) return target;
            } 
            
            return null;
		}
        

        internal static void SetChild(GameObject parent, GameObject child)
        {
            child.layer                   = parent.layer;
            child.transform.parent        = parent.transform;
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale    = Vector3.one;
            child.transform.rotation      = Quaternion.identity;
        }

        internal static GameObject SetCloneChild(GameObject parent, GameObject orignal, string name)
        {
			GameObject clone = UnityEngine.Object.Instantiate(orignal) as GameObject;
			if (!clone) return null;

			clone.name = name;
			SetChild(parent, clone);

			return clone;
		}
		
		internal static void ReleaseChild(GameObject child)
		{
            child.transform.parent = null;
            child.SetActive(false);
		}
		
        internal static void DestoryChild(GameObject parent, string name)
        {
            GameObject child = FindChild(parent, name);
            if (child) 
            {
				child.transform.parent = null;
	            GameObject.Destroy(child);
	        }
        }

        internal static UIAtlas FindAtlas(string s)
		{
			return ( (new List<UIAtlas>( Resources.FindObjectsOfTypeAll<UIAtlas>() )).FirstOrDefault(a => a.name == s)  );
		}

        internal static void WriteTrans(string s)
        {
            GameObject go = GameObject.Find(s);
            if (!go) return;

            WriteTrans(go.transform, 0, null);
        }
        internal static void WriteTrans(Transform t) { WriteTrans(t, 0, null); }
        internal static void WriteTrans(Transform t, int level, StreamWriter writer)
        {
            if (level == 0) writer = new StreamWriter(@".\"+ t.name +@".txt", false);
            if (writer == null) return;
            
            string s = "";
            for(int i=0; i<level; i++) s+="    ";
            writer.WriteLine(s + level +","+t.name);
            foreach (Transform tc in t)
            {
                WriteTrans(tc, level+1, writer);
            }

            if (level == 0) writer.Close();
        }

        internal static void WriteChildrenComponent(GameObject go)
        {
			WriteComponent(go);
			
            foreach (Transform tc in go.transform)
            {
                WriteChildrenComponent(tc.gameObject);
            }
		}

        internal static void WriteComponent(GameObject go)
        {
            Component[] compos = go.GetComponents<Component>();
            foreach(Component c in compos){ Debug.Log(go.name +":"+ c.GetType().Name); }
        }

        #endregion
    }
}

