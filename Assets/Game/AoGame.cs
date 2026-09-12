using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Aoerlunduo
{
    public class AoGame : MonoBehaviour
    {
        public AoSimulation Sim {get;private set;}
        public int Selected {get;private set;} = 3;
        public int ArmySource {get;private set;} = -1;
        public int Speed {get;private set;}
        float timer;
        TMP_FontAsset font;
        TextMeshProUGUI heading,stats,detail,notice,eventText,speedText;
        RectTransform map,root;
        GameObject eventPanel;
        UnityEngine.UI.Button select, farm, fort, recruit, diplomacy, march, source;
        readonly System.Collections.Generic.List<UnityEngine.UI.Button> markers=new System.Collections.Generic.List<UnityEngine.UI.Button>();
        IDisposable subscription;
        public string SavePath=>Path.Combine(Application.persistentDataPath,"aoerlunduo-v1.json");
        static readonly Color Ink=new Color(.035f,.065f,.10f), Panel=new Color(.07f,.115f,.16f), Gold=new Color(.88f,.72f,.40f), White=new Color(.90f,.93f,.93f);
        void Awake()
        {
            Application.targetFrameRate=60;Screen.orientation=ScreenOrientation.LandscapeLeft;
            Sim=new AoSimulation();var f=Resources.Load<Font>("NotoSansCJKsc-Regular");
            font=TMP_FontAsset.CreateFontAsset(f,32,4,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048);
            font.isMultiAtlasTexturesEnabled=true;
            MakeUI();subscription=Sim.Events.Subscribe<WorldChanged>(_=>Refresh());Refresh();
        }
        void OnDestroy(){subscription?.Dispose();Sim?.Dispose();if(font!=null)Destroy(font);}
        void Update()
        {
            if(Speed>0&&Sim.State.player>=0&&Sim.State.pendingEvent<0){timer+=Time.unscaledDeltaTime*Speed;if(timer>=3){timer-=3;Sim.Tick();}}
            if(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame)SetSpeed(Speed==0?1:0);
            var area=Screen.safeArea;root.anchorMin=new Vector2(area.x/Screen.width,area.y/Screen.height);root.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
        }
        RectTransform Box(string name,Transform parent,float x,float y,float w,float h,Color? color=null)
        {
            var o=new GameObject(name,typeof(RectTransform));var r=o.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
            if(color.HasValue)o.AddComponent<UnityEngine.UI.Image>().color=color.Value;return r;
        }
        TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,int size=20,Color? color=null)
        {
            var r=Box("Label",parent,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color??White;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;
        }
        UnityEngine.UI.Button Button(Transform parent,string text,float x,float y,float w,float h,Action action,Color? color=null)
        {
            var r=Box(text,parent,x,y,w,h,color??new Color(.12f,.20f,.26f));var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=r.GetComponent<UnityEngine.UI.Image>();b.onClick.AddListener(()=>action());var t=Label(r,text,7,3,w-14,h-6,19);t.alignment=TextAlignmentOptions.Center;return b;
        }
        void MakeUI()
        {
            var canvas=new GameObject("AoCanvas",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvas.transform.SetParent(transform,false);var display=canvas.GetComponent<Canvas>();display.renderMode=RenderMode.ScreenSpaceCamera;display.worldCamera=Camera.main;display.planeDistance=1;
            var scale=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,900);scale.matchWidthOrHeight=.5f;
            root=Box("SafeArea",canvas.transform,0,0,0,0);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            var design=Box("Design",root,0,0,1600,900,Ink);design.anchorMin=design.anchorMax=design.pivot=new Vector2(.5f,.5f);design.anchoredPosition=Vector2.zero;
            var fit=design.gameObject.AddComponent<AoFit>();fit.parent=root;
            Box("TopBar",design,0,0,1600,94,Panel);Label(design,"奥 尔 伦 多",26,13,280,42,33,Gold);Label(design,"世界战略 · 首版原型 0.1",28,61,290,25,15);
            heading=Label(design,"",340,16,640,31,23,Gold);stats=Label(design,"",340,54,770,27,18);
            Button(design,"暂停",1130,24,78,44,()=>SetSpeed(0));Button(design,"1×",1216,24,60,44,()=>SetSpeed(1));Button(design,"3×",1284,24,60,44,()=>SetSpeed(3));Button(design,"6×",1352,24,60,44,()=>SetSpeed(6));speedText=Label(design,"已暂停",1428,32,150,35,18,Gold);
            var viewport=Box("WorldViewport",design,22,114,1128,696,new Color(.025f,.085f,.12f));viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            map=Box("WorldMap",viewport,0,0,1128,734);map.anchorMin=map.anchorMax=map.pivot=new Vector2(.5f,.5f);map.anchoredPosition=Vector2.zero;
            var image=map.gameObject.AddComponent<UnityEngine.UI.RawImage>();image.texture=Resources.Load<Texture2D>("AoPoliticalMap");
            var drag=map.gameObject.AddComponent<AoMapInput>();drag.map=map;drag.viewport=viewport;
            for(int i=0;i<Sim.State.nations.Count;i++)
            {
                int id=i;var n=Sim.State.nations[i];var b=Button(map,(i+1).ToString("00"),n.x*1128-14,n.y*734-14,29,29,()=>Inspect(id),NationColor(i));
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize=12;var trigger=b.gameObject.AddComponent<AoMarkerDrag>();trigger.input=drag;markers.Add(b);
            }
            Label(design,"点击编号查看国家  /  拖动地图  /  滚轮或双指缩放",30,822,820,26,17);
            Button(design,"−",980,818,48,38,()=>drag.Zoom(-.25f));Button(design,"＋",1036,818,48,38,()=>drag.Zoom(.25f));Button(design,"复位",1092,818,58,38,()=>drag.ResetView());
            var side=Box("CountryPanel",design,1170,114,408,696,Panel);Label(side,"国家档案",22,17,340,40,26,Gold);
            Button(side,"‹",22,65,45,36,()=>Inspect((Selected+27)%28));Button(side,"›",339,65,45,36,()=>Inspect((Selected+1)%28));
            detail=Label(side,"",23,112,360,260,20);
            select=Button(side,"选择该国 · 开始游戏",22,380,362,47,()=>{if(Sim.Select(Selected))SetSpeed(0);},new Color(.37f,.29f,.13f));
            farm=Button(side,"扩建农田  70金",22,437,175,44,()=>Act(Sim.Build(Selected,false)));
            fort=Button(side,"修筑要塞  90金",207,437,177,44,()=>Act(Sim.Build(Selected,true)));
            recruit=Button(side,"招募5军团  50金",22,491,362,44,()=>Act(Sim.Recruit(Selected)));
            source=Button(side,"选定出发军队",22,545,175,44,()=>{ArmySource=Selected;Sim.Changed("已选定 "+Sim.State.nations[Selected].name+" 的军队，请选择相邻目的地。");});
            march=Button(side,"行军 / 进攻",207,545,177,44,()=>Act(Sim.March(ArmySource,Selected)));
            diplomacy=Button(side,"宣战 / 停战",22,599,362,44,()=>Act(Sim.Diplomacy(Selected)));
            Label(side,"国界、首都落点与陆路连接暂定",22,656,370,24,14,new Color(.62f,.7f,.73f));
            notice=Label(design,"",27,863,1110,30,18,Gold);Button(design,"保存",1170,822,128,42,Save);Button(design,"读取",1310,822,128,42,Load);Button(design,"下个月",1450,822,128,42,()=>{SetSpeed(0);Sim.Tick();});
            eventPanel=Box("EventModal",design,425,220,700,370,Panel).gameObject;Label(eventPanel.transform,"奥尔伦多纪事",30,25,640,45,29,Gold);eventText=Label(eventPanel.transform,"",30,88,640,150,23);
            Button(eventPanel.transform,"投入40金",30,281,302,55,()=>Sim.ChooseEvent(true));Button(eventPanel.transform,"暂缓投入 · 获得15金",352,281,318,55,()=>Sim.ChooseEvent(false));eventPanel.SetActive(false);
            if(EventSystem.current==null){var es=new GameObject("AoEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(transform,false);}
        }
        public static Color NationColor(int i)=>Color.HSVToRGB((i*.618034f)%1,.47f,.70f);
        public void Inspect(int id){Selected=id;Refresh();}
        void LateUpdate(){if(map==null)return;for(int i=0;i<markers.Count;i++)markers[i].transform.localScale=Vector3.one*(i==Selected?1.35f:1)/map.localScale.x;}
        public void SetSpeed(int speed){Speed=speed;timer=0;Refresh();}
        void Act(bool result){if(!result)Sim.Changed("无法执行：请检查归属、国库、兵力、陆路邻接及战争状态。");}
        public void Refresh()
        {
            if(detail==null)return;var s=Sim.State;var n=s.nations[Selected];var owner=s.nations[n.owner];bool started=s.player>=0,owned=started&&n.owner==s.player;
            heading.text=$"AO 纪元 {s.year} 年  {s.month:00} 月    ·    "+(started?s.nations[s.player].name:"选择你的国家");
            stats.text=started?$"国库 {s.nations[s.player].Gold.ToInt()} 金     月度净收入 {Sim.Income(s.player):+0;-0;0}     领土 {Sim.Territory(s.player)}     军团 {s.nations.Where(t=>t.owner==s.player).Sum(t=>t.troops)}":"28个暂定国家  ·  四片主要陆地  ·  单人离线";
            speedText.text=Speed==0?"已暂停":$"{Speed}× 运行";
            detail.text=$"<color=#E0B866><size=28>{n.name}</size></color>  {n.id}\n首都  {n.capital}\n控制国  {owner.name}\n人口  {n.population} 万\n驻军  {n.troops} 军团\n农田 {n.farms}  ·  要塞 {n.forts}\n邻接  {string.Join("、",n.neighbors.Select(i=>s.nations[i].name))}";
            select.gameObject.SetActive(!started);farm.interactable=fort.interactable=recruit.interactable=source.interactable=owned&&s.pendingEvent<0;
            march.interactable=started&&ArmySource>=0&&ArmySource!=Selected&&s.nations[ArmySource].owner==s.player&&s.nations[ArmySource].neighbors.Contains(Selected)&&s.pendingEvent<0;
            diplomacy.interactable=started&&!owned&&s.pendingEvent<0;
            notice.text=Sim.Notice;
            for(int i=0;i<markers.Count;i++){var m=markers[i];m.GetComponent<UnityEngine.UI.Image>().color=i==Selected?Gold:NationColor(s.nations[i].owner);m.transform.localScale=Vector3.one*(i==Selected?1.35f:1);}
            eventPanel.SetActive(s.pendingEvent>=0);if(s.pendingEvent>=0){string[] events={"粮食丰收\n扩建粮仓和灌渠，可永久增加一处农田。","流民抵达\n提供安置资金，可增加30万人口。","边防动员\n投资训练与装备，可获得5个军团。"};eventText.text=events[s.pendingEvent]+"\n\n事件处理期间，时间暂停。";}
            if(started&&Sim.Territory(s.player)==0)Speed=0;
        }
        public void Save()
        {
            if(Sim.State.player<0){Sim.Changed("请先选择国家。");return;}
            try{string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(Sim.State,true));if(File.Exists(SavePath))File.Copy(SavePath,SavePath+".bak",true);File.Copy(temp,SavePath,true);File.Delete(temp);Sim.Changed("存档已保存。");}catch(Exception e){Sim.Changed("保存失败："+e.Message);}
        }
        public void Load()
        {
            try{var loaded=JsonUtility.FromJson<Campaign>(File.ReadAllText(SavePath));if(!AoSimulation.Validate(loaded))throw new Exception("存档格式或数据不兼容");Sim.State=loaded;ArmySource=-1;SetSpeed(0);Sim.Changed("已读取存档，时间暂停。");}catch(Exception e){Sim.Changed("读取失败："+e.Message);}
        }
    }
    public class AoFit:MonoBehaviour
    {
        public RectTransform parent;
        void LateUpdate(){float s=Mathf.Min(parent.rect.width/1600,parent.rect.height/900);transform.localScale=Vector3.one*s;}
    }
    public class AoMapInput:MonoBehaviour,IDragHandler,IScrollHandler
    {
        public RectTransform map,viewport;
        public void OnDrag(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,e.position,e.pressEventCamera,out var a);RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,e.position-e.delta,e.pressEventCamera,out var b);map.anchoredPosition+=a-b;Clamp();}
        public void OnScroll(PointerEventData e)=>Zoom(e.scrollDelta.y*.15f);
        public void Zoom(float delta){map.localScale=Vector3.one*Mathf.Clamp(map.localScale.x+delta,1,5);Clamp();}
        public void ResetView(){map.localScale=Vector3.one;map.anchoredPosition=Vector2.zero;}
        void Clamp(){var limit=(map.sizeDelta*map.localScale.x-viewport.rect.size)*.5f;map.anchoredPosition=new Vector2(Mathf.Clamp(map.anchoredPosition.x,-Mathf.Max(0,limit.x),Mathf.Max(0,limit.x)),Mathf.Clamp(map.anchoredPosition.y,-Mathf.Max(0,limit.y),Mathf.Max(0,limit.y)));}
        float lastPinch;
        void Update(){var ts=Touchscreen.current;if(ts==null||!ts.touches[0].press.isPressed||!ts.touches[1].press.isPressed){lastPinch=0;return;}var a=ts.touches[0].position.ReadValue();var b=ts.touches[1].position.ReadValue();if(!RectTransformUtility.RectangleContainsScreenPoint(viewport,a)||!RectTransformUtility.RectangleContainsScreenPoint(viewport,b))return;float d=Vector2.Distance(a,b);if(lastPinch>0)Zoom((d-lastPinch)*.005f);lastPinch=d;}
    }
    public class AoMarkerDrag:MonoBehaviour,IDragHandler,IScrollHandler
    {
        public AoMapInput input;public void OnDrag(PointerEventData e){input.OnDrag(e);e.eligibleForClick=false;}public void OnScroll(PointerEventData e)=>input.OnScroll(e);
    }
}
