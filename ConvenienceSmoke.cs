using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using SephiriaDicePreview;
using SephiriaPersonalConvenience;
[BepInPlugin("local.sephiria.convenience-smoke", "Convenience Smoke", "1.0.0")]
public sealed class ConvenienceSmoke : BaseUnityPlugin {
 private string root;
 private PlayerAvatar Player { get { return PlayerSpawner.MultiplayerList.Where(p=>p && p.isLocalPlayer).Select(p=>p.PlayerAvatar).FirstOrDefault(); } }
 private void Awake() {
  root=Environment.GetEnvironmentVariable("SEPHIRIA_COMBAT_TEST_ROOT");
  if(String.IsNullOrEmpty(root)||Path.GetFullPath(SaveData.CommonPath)!=Path.Combine(root,"isolated-saves"))throw new Exception("Isolation absent");
  if(UnityEngine.InputSystem.Keyboard.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
  if(UnityEngine.InputSystem.Mouse.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
  File.WriteAllText(Path.Combine(root,"dice-smoke.txt"),""); StartCoroutine(Guard());
 }
 private void Log(string v){File.AppendAllText(Path.Combine(root,"dice-smoke.txt"),v+"\n");}
 private IEnumerator Guard(){var r=Run();while(true){bool n;try{n=r.MoveNext();}catch(Exception e){Log("FAIL "+e);Application.Quit(21);yield break;}if(!n)break;yield return r.Current;}}
 private string Signature(SephiriteRewardMetadata[] r){return String.Join(",",r.Select(x=>x.entityID+":"+x.instanceID).ToArray());}
 private IEnumerator Run(){
  float end=Time.realtimeSinceStartup+90;
  while((!Player||!Player.CanMove||Player.loadingScreenType!=-1)&&Time.realtimeSinceStartup<end)yield return null;
  if(!Player||!Player.CanMove)throw new Exception("Startup timeout");
  OptionsBinding.Instance.Options.SetBool("Photosensitive_NeverShowAgain",true);
  OptionsBinding.Instance.Options.SetBool("DataCollectionAgreementInitialized",true);
  OptionsBinding.Instance.Options.SetString("GamePlayLanguage","ko-KR");LocalizationManager.Instance.LoadLanguage("ko-KR");
  SaveManager.Current.SetBool("ReplenishmentTutorial_InShop",false);SaveManager.Current.SetBool("SubBagTutorial",false);SaveManager.Current.SetBool("SephiriteOpenVeryFirst",false);SaveManager.Current.SetBool("ShowDiceConvertTutorial",false);
  var level=Player.GetComponent<LevelController>();
  var ids=(HashSet<int>)AccessTools.Field(typeof(DungeonManager),"issuedInstanceID").GetValue(DungeonManager.Instance);
  foreach(var type in new[]{Sephirite.Type.CHARM,Sephirite.Type.TABLET,Sephirite.Type.NORMAL,Sephirite.Type.BIG,Sephirite.Type.BOSS,Sephirite.Type.TABLET_BOSS}){
   level.GenerateItem(123456);var src=level.levelUpQueue.Last();src.type=type;src.isSkipOpenAnimation=true;src.GenerateItems(Player.gameObject);
   for(int i=0;i<5;i++){
    Player.NetworkrerollDice=10;
    var before=Signature(src.rewards.ToArray());var appeared=src.appearedItems.ToArray();int seed=src.CurrentSeed,count=ids.Count,free=src.freeRerolledCounter;
    var next=Predictor.Next(src,Player);var again=Predictor.Next(src,Player);
    if(next.Length==0||Signature(next)!=Signature(again))throw new Exception("Unstable/empty prediction");
    if(before!=Signature(src.rewards.ToArray())||!appeared.SequenceEqual(src.appearedItems)||seed!=src.CurrentSeed||count!=ids.Count||free!=src.freeRerolledCounter||Player.rerollDice!=10)throw new Exception("Prediction mutated state");
    src.Reroll(Player.gameObject);
    if(Signature(next)!=Signature(src.rewards.ToArray()))throw new Exception("Mismatch: "+Signature(next)+" vs "+Signature(src.rewards.ToArray()));
    Log("PASS "+type+" seed="+seed+" state unchanged and actual="+Signature(next));
   }
      string selectedCategory = src.rewards.SelectMany(r=>ItemDatabase.FindItemById(r.entityID).categories).FirstOrDefault() ?? "NO_CATEGORY";
   Player.localDataStorage.fruitSkewerBonus.Clear();Player.localDataStorage.fruitSkewerBonus.Add(new GridInventory.ItemDropBonusData{categoryName=selectedCategory,weight=1});
   if(type==Sephirite.Type.CHARM||type==Sephirite.Type.TABLET)level.Open();else Player.AcquireSephiriteReward(src,null);yield return new WaitForSecondsRealtime(5);
   var panel=UIManager.Instance.GetElement<UI_SephiriteRewardPanel>();
         var nativeRewards=panel.GetComponentsInChildren<UI_SephiriteRewardElement>();
   foreach(var element in nativeRewards){bool expected=ItemDatabase.FindItemById(element.reward.entityID).categories.Contains(selectedCategory);var outline=element.GetComponent<ComboOutline>();if(!outline||outline.Highlighted!=expected)throw new Exception("Reward combo outline mismatch");}
   Log("PASS reward outlines "+type+" category="+selectedCategory);
   var preview=panel.GetComponent<RewardPreview>();
   if(!preview||!preview.PreviewRoot||!preview.PreviewRoot.activeInHierarchy)throw new Exception("Preview not visible");
   if(Signature(preview.Displayed)!=Signature(Predictor.Next(src,Player)))throw new Exception("Displayed result mismatch");
   var previewCards=nativeRewards.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("NextDicePreviewItem")).ToArray();
   if(previewCards.Length!=Math.Min(preview.Displayed.Length,nativeRewards.Length))throw new Exception("Overlapped preview card count mismatch");
   if(previewCards.Any(c=>c.GetComponentInChildren<TMPro.TMP_Text>(true)))throw new Exception("Preview item name was not removed");
   if(previewCards.Any(c=>((RectTransform)c).offsetMin.x>-19))throw new Exception("Preview item is not exposed on the left");
   var charmRewards=nativeRewards.Where(r=>ItemDatabase.FindItemById(r.reward.entityID).type==EItemType.Charm).ToArray();
   foreach(var reward in nativeRewards){var badge=reward.GetComponent<RewardDpsBadge>();if(!badge)throw new Exception("Reward DPS component missing");if(ItemDatabase.FindItemById(reward.reward.entityID).type!=EItemType.Charm&&badge.Eligible)throw new Exception("Non-artifact DPS badge visible");}
   if(charmRewards.Length>0)
   {
    float dpsDeadline=Time.realtimeSinceStartup+20;while(charmRewards.Any(r=>!r.GetComponent<RewardDpsBadge>().Eligible)&&Time.realtimeSinceStartup<dpsDeadline)yield return null;
    if(charmRewards.Any(r=>!r.GetComponent<RewardDpsBadge>().Eligible))throw new Exception("Artifact DPS prediction timeout");
    int recommended=charmRewards.Count(r=>r.transform.Find("DpsRecommendation")&&r.transform.Find("DpsRecommendation").gameObject.activeSelf);
    if(recommended!=1)throw new Exception("Recommendation marker count="+recommended);
   }
   Capture(panel,"dice-"+type+".png");
   if(type==Sephirite.Type.CHARM){
    var magicPreview=Resources.LoadAll<ItemEntity>("Item").First(i=>i.type==EItemType.Charm&&i.resourcePrefab&&i.resourcePrefab.GetComponent<Charm_Magic>()&&i.icon);
    var originalPreview=preview.Displayed[0];
    preview.Displayed[0]=new SephiriteRewardMetadata(preview.Displayed[0].instanceID,magicPreview.id);
    AccessTools.Method(typeof(RewardPreview),"Draw").Invoke(preview,null);yield return null;
    Capture(panel,"dice-magic-preview.png");
    preview.Displayed[0]=originalPreview;AccessTools.Method(typeof(RewardPreview),"Draw").Invoke(preview,null);yield return null;
    var opts=UIManager.Instance.GetElement<UI_OptionsPanel>();opts.Open();yield return new WaitForSecondsRealtime(.5f);
        var setting=opts.GetComponent<PreviewOptions>();if(!setting||!setting.Box||!setting.RewardDpsBox)throw new Exception("Option missing");
    if(opts.tab.tabButtons.Length!=6||setting.TabIndex!=5)throw new Exception("Tab count/index mismatch");
    var tabButton=opts.tab.tabButtons[5];var pointer=tabButton as UI_TabPointClickableButton;
    if(pointer)pointer.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));else tabButton.GetComponent<Button>().onClick.Invoke();
    yield return new WaitForSecondsRealtime(.3f);
    if(opts.tab.CurrentSelectedTab!=5||!setting.Box.gameObject.activeInHierarchy)throw new Exception("Extras tab click failed");
    setting.ComboBox.ChangeValue(0);yield return new WaitForSecondsRealtime(.2f);
    if(nativeRewards.Any(r=>r.GetComponent<ComboOutline>().Highlighted))throw new Exception("Combo off failed");
    setting.ComboBox.ChangeValue(1);yield return new WaitForSecondsRealtime(.2f);
    if(!nativeRewards.Any(r=>r.GetComponent<ComboOutline>().Highlighted))throw new Exception("Combo on failed");
    Log("PASS sixth tab click and combo toggle");
    setting.Box.ChangeValue(0);yield return new WaitForSecondsRealtime(.3f);
    if(preview.PreviewRoot.activeInHierarchy||PersonalConveniencePlugin.Instance.ShowPreview.Value)throw new Exception("Off failed");
    PersonalConveniencePlugin.Instance.Config.Reload();if(PersonalConveniencePlugin.Instance.ShowPreview.Value)throw new Exception("Off not saved");
    Capture(opts,"dice-options-off.png");opts.Close();yield return new WaitForSecondsRealtime(.2f);Capture(panel,"dice-preview-off.png");
    opts.Open();opts.SelectTab(setting.TabIndex);yield return new WaitForSecondsRealtime(.2f);setting.Box.ChangeValue(1);yield return new WaitForSecondsRealtime(.3f);
    PersonalConveniencePlugin.Instance.Config.Reload();if(!PersonalConveniencePlugin.Instance.ShowPreview.Value)throw new Exception("On not saved");
    Capture(opts,"dice-options-on.png");opts.Close();yield return new WaitForSecondsRealtime(.3f);
    if(!preview.PreviewRoot.activeInHierarchy)throw new Exception("On failed");
    Log("PASS native option off/on, persisted reload, preview visibility");
   }
   var shown=Signature(preview.Displayed);panel.Reroll();yield return new WaitForSecondsRealtime(.5f);
   if(Signature(src.rewards.ToArray())!=shown)throw new Exception("UI reroll did not match");
   if(Signature(preview.Displayed)!=Signature(Predictor.Next(src,Player)))throw new Exception("Preview stale after reroll");
   Log("PASS displayed UI result and refresh after reroll "+type);
   Log("panel="+panel.IsOpened+" ready="+panel.rewardsGroupInteractable+" rect="+((RectTransform)panel.transform).rect);
   foreach(var t in panel.GetComponentsInChildren<TMPro.TMP_Text>())Log("text="+t.text+" pos="+t.transform.position);
   panel.Close();level.levelUpQueue.Clear();Mirror.NetworkServer.Destroy(src.gameObject);
   if(type==Sephirite.Type.CHARM&&Environment.GetEnvironmentVariable("SEPHIRIA_REWARD_UI_ONLY")=="1")
   {Log("PASS REWARD UI COMPLETE");Application.Quit(0);yield break;}
  }
  level.GenerateItem(54321);var general=level.levelUpQueue.Last();general.type=Sephirite.Type.CHARM;general.isSkipOpenAnimation=true;general.GenerateItems(Player.gameObject);
  string generalCategory=general.rewards.SelectMany(r=>ItemDatabase.FindItemById(r.entityID).categories).First();
  Player.localDataStorage.fruitSkewerBonus.Clear();Player.localDataStorage.fruitSkewerBonus.Add(new GridInventory.ItemDropBonusData{categoryName=generalCategory,weight=1});
  Player.AcquireSephiriteReward(general,null);yield return new WaitForSecondsRealtime(3);
  var generalPanel=UIManager.Instance.GetElement<UI_SephiriteRewardPanel>();
  if(!generalPanel.GetComponentsInChildren<ComboOutline>().Any(o=>o.Highlighted))throw new Exception("Non-level-up artifact outline absent");
  if(!generalPanel.GetComponent<RewardPreview>().PreviewRoot.activeInHierarchy)throw new Exception("Preview missing in non-level-up reward");
  Capture(generalPanel,"combo-artifact-reward.png");Log("PASS general artifact reward outline and preview");
  generalPanel.Close();level.levelUpQueue.Clear();Mirror.NetworkServer.Destroy(general.gameObject);
  var artifacts=Resources.LoadAll<ItemEntity>("Item").Where(i=>i.type==EItemType.Charm&&i.activeType==EItemActiveType.Default&&i.categories.Count>0).ToArray();
  var match=artifacts.First();string category=match.categories[0];var noMatch=artifacts.First(i=>!i.categories.Contains(category));
  Player.localDataStorage.fruitSkewerBonus.Clear();Player.localDataStorage.fruitSkewerBonus.Add(new GridInventory.ItemDropBonusData{categoryName=category,weight=1});
  int originalDungeon=Player.isInDungeon;var oldConsumed=Player.spawner.consumeFruitSkewerBonus.ToArray();
  try {
   Player.isInDungeon=1;Player.spawner.consumeFruitSkewerBonus.Clear();Player.spawner.consumeFruitSkewerBonus.Add(new GridInventory.ItemDropBonusData{categoryName=category,weight=-1});
   if(FruitCombos.Matches(match,Player))throw new Exception("Used future settings instead of consumed skewer");
   Player.spawner.consumeFruitSkewerBonus.Clear();Player.spawner.consumeFruitSkewerBonus.Add(new GridInventory.ItemDropBonusData{categoryName=category,weight=1});
   if(!FruitCombos.Matches(match,Player))throw new Exception("Consumed skewer not used");
  } finally {Player.isInDungeon=originalDungeon;Player.spawner.consumeFruitSkewerBonus.Clear();foreach(var bonus in oldConsumed)Player.spawner.consumeFruitSkewerBonus.Add(bonus);}
  Log("PASS current-run consumed skewer overrides future preference");
  Player.Inventory.AddItem(new ItemMetadata(190010001,match.id,1),0,false);Player.Inventory.AddItem(new ItemMetadata(190010002,noMatch.id,1),0,false);
  yield return new WaitForSecondsRealtime(.5f);
  var shop=UIManager.Instance.GetElement<UI_ShopPanel>();shop.Open("TEST",Player,Player.Inventory,"상점 강조 테스트",Player,Player.Inventory,ETradeType.BuyOnly,"");
  yield return new WaitForSecondsRealtime(1);
  int marked=0,unmarked=0;
  foreach(var icon in shop.shopInventoryIconList.Where(i=>i.gameObject.activeInHierarchy&&i.Item!=null)){
   bool expected=icon.Item.Entity.categories.Contains(category);var outline=icon.GetComponent<ComboOutline>();
   if(!outline||outline.Highlighted!=expected)throw new Exception("Shop combo outline mismatch");
   if(expected)marked++;else unmarked++;
  }
  if(marked==0||unmarked==0)throw new Exception("Shop fixture coverage missing");
  Capture(shop,"combo-shop.png");Log("PASS native shop outlines matched="+marked+" unmatched="+unmarked);
  Player.localDataStorage.fruitSkewerBonus.Clear();Player.localDataStorage.fruitSkewerBonus.Add(new GridInventory.ItemDropBonusData{categoryName=category,weight=-1});
  yield return new WaitForSecondsRealtime(.3f);
  if(shop.shopInventoryIconList.Any(i=>i.GetComponent<ComboOutline>()&&i.GetComponent<ComboOutline>().Highlighted))throw new Exception("Negative category was highlighted");
  Log("PASS negative preference removes outline");shop.Close();
  var options=UIManager.Instance.GetElement<UI_OptionsPanel>();options.Open();options.SelectTab(options.GetComponent<PreviewOptions>().TabIndex);yield return new WaitForSecondsRealtime(1);
  foreach(var row in options.GetComponentsInChildren<UI_OptionBox_Common_Integer>(true))Log("option="+row.optionKey+" parent="+row.transform.parent.name);
  Capture(options,"dice-options.png");
  for(int i=0;i<options.tab.tabButtons.Length;i++){
   options.SelectTab(i);yield return new WaitForSecondsRealtime(.1f);
   var tabImage=options.tab.tabButtons[i].butttonImage;
   var eventData=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);eventData.position=RectTransformUtility.WorldToScreenPoint(null,tabImage.rectTransform.position);
   var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData,hits);
   if(!hits.Any(h=>h.gameObject.GetComponentInParent<UI_TabButton>()==options.tab.tabButtons[i]))throw new Exception("Tab not raycastable "+i);
   if(!options.tab.tabContents[i].gameObject.activeInHierarchy||options.tab.tabContents.Where((c,j)=>j!=i).Any(c=>c.gameObject.activeSelf))throw new Exception("Tab switch failed "+i);
   Capture(options,"options-tab-"+i+".png");
  }
  Log("PASS all six options tabs preserve switching and content");
  Log("PASS COMPLETE");Application.Quit(0);
 }    private void Capture(UIBase panel, string file)
    {
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        var cameraObject = new GameObject("RetryProbeCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 400;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = new RenderTexture(1280, 800, 24);
        camera.targetTexture = target;
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var capture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); capture.Apply();
        File.WriteAllBytes(Path.Combine(root, file), capture.EncodeToPNG());
        RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
        Destroy(capture); Destroy(cameraObject); target.Release(); Destroy(target);
    }
}







