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
[BepInPlugin("local.sephiria.other-dice-smoke", "Convenience Smoke", "1.0.0")]
public sealed class OtherSmoke : BaseUnityPlugin {
 private string root;
 private PlayerAvatar Player { get { return PlayerSpawner.MultiplayerList.Where(p=>p && p.isLocalPlayer).Select(p=>p.PlayerAvatar).FirstOrDefault(); } }
 private void Awake() {
  root=Environment.GetEnvironmentVariable("SEPHIRIA_COMBAT_TEST_ROOT");
  if(String.IsNullOrEmpty(root)||Path.GetFullPath(SaveData.CommonPath)!=Path.Combine(root,"isolated-saves"))throw new Exception("Isolation absent");
  if(UnityEngine.InputSystem.Keyboard.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
  if(UnityEngine.InputSystem.Mouse.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
  File.WriteAllText(Path.Combine(root,"other-dice-smoke.txt"),""); StartCoroutine(Guard());
 }
 private void Log(string v){File.AppendAllText(Path.Combine(root,"other-dice-smoke.txt"),v+"\n");}
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
  Player.NetworkmaxRerollDice=10;Player.NetworkrerollDice=10;
  var go=new GameObject("MiracleTest");go.SetActive(false);go.AddComponent<Mirror.NetworkIdentity>();var interact=go.AddComponent<Interactable>();var selector=go.AddComponent<MiracleSelector2>();selector.interactable=interact;selector.fruitObj=new GameObject("Fruit");selector.fruitObj.transform.SetParent(go.transform);go.SetActive(true);Mirror.NetworkServer.Spawn(go);selector.SetRandomID(384721);
  var actor=Player.GetComponent<MiracleController>();AccessTools.Method(typeof(MiracleSelector2),"LocalRequestMiracle").Invoke(selector,new object[]{actor});yield return new WaitForSecondsRealtime(1);
  var miraclePanel=UIManager.Instance.GetElement<UI_MiraclePanel>();
  for(int i=0;i<4;i++){
   var preview=miraclePanel.GetComponent<OtherDicePreview>();if(!preview||!preview.PreviewRoot||!preview.PreviewRoot.activeInHierarchy)throw new Exception("Miracle preview missing");
   int dice=Player.rerollDice;var requested=OtherDicePredictor.Field<Dictionary<Mirror.NetworkIdentity,List<Miracle>>>(selector,"requestedMiracles")[actor.netIdentity].Select(m=>m.id).ToArray();
   var counts=OtherDicePredictor.Field<Dictionary<Mirror.NetworkIdentity,int>>(selector,"rerolledCount");int count=counts.ContainsKey(actor.netIdentity)?counts[actor.netIdentity]:0;
   var predicted=OtherDicePredictor.MiracleNext(selector,actor);var again=OtherDicePredictor.MiracleNext(selector,actor);
   if(!predicted.Select(m=>m.id+":"+m.instanceID).SequenceEqual(again.Select(m=>m.id+":"+m.instanceID))||!requested.SequenceEqual(OtherDicePredictor.Field<Dictionary<Mirror.NetworkIdentity,List<Miracle>>>(selector,"requestedMiracles")[actor.netIdentity].Select(m=>m.id))||Player.rerollDice!=dice||(counts.ContainsKey(actor.netIdentity)?counts[actor.netIdentity]:0)!=count)throw new Exception("Miracle prediction mutated state");
   if(!preview.Miracles.Select(m=>m.id).SequenceEqual(predicted.Select(m=>m.id)))throw new Exception("Miracle displayed mismatch");
   Capture(miraclePanel,"dice-miracle.png");miraclePanel.RequestReroll();yield return new WaitForSecondsRealtime(.4f);
   var actual=(MiracleMetadata[])AccessTools.Method(typeof(MiracleSelector2),"GenerateMiracles").Invoke(selector,new object[]{actor.netIdentity,Player.RandomID,Player});
   if(!predicted.Select(m=>m.id+":"+m.instanceID).SequenceEqual(actual.Select(m=>m.id+":"+m.instanceID)))throw new Exception("Miracle reroll mismatch");
   Log("PASS miracle "+i+" prediction read-only, displayed and actual="+String.Join(",",actual.Select(m=>m.id).ToArray()));
  }
  PersonalConveniencePlugin.Instance.ShowPreview.Value=false;yield return new WaitForSecondsRealtime(.2f);if(miraclePanel.GetComponent<OtherDicePreview>().PreviewRoot.activeInHierarchy)throw new Exception("Miracle off failed");PersonalConveniencePlugin.Instance.ShowPreview.Value=true;yield return new WaitForSecondsRealtime(.2f);
  miraclePanel.Close();Mirror.NetworkServer.Destroy(go);
  Player.AddCustomStatUnsafe("EXTRAWEAPONCHOICES",1);
  var baseWeapon=WeaponDatabase.GetBaseWeapons().First(w=>WeaponDatabase.GetWeaponEnhancements(w.id)!=null&&WeaponDatabase.GetWeaponEnhancements(w.id).Count>3);
  Player.GetComponent<WeaponControllerSimple>().EquipWeapon(false,baseWeapon.id);yield return new WaitForSecondsRealtime(1);
  var anvilObject=new GameObject("AnvilTest");anvilObject.SetActive(false);anvilObject.AddComponent<Mirror.NetworkIdentity>();anvilObject.AddComponent<Interactable>();var anvil=anvilObject.AddComponent<Anvil>();anvil.SetRandomID(92841);anvilObject.SetActive(true);Mirror.NetworkServer.Spawn(anvilObject);
  AccessTools.Method(typeof(Anvil),"HandleInteraction").Invoke(anvil,new object[]{Player.gameObject});yield return new WaitForSecondsRealtime(1);
  var weaponPanel=UIManager.Instance.GetElement<UI_WeaponEnhancementPanel>();
  for(int i=0;i<4;i++){
   Player.NetworkrerollDice=10;var preview=weaponPanel.GetComponent<OtherDicePreview>();if(!preview||!preview.PreviewRoot||!preview.PreviewRoot.activeInHierarchy)throw new Exception("Weapon preview missing");
   var candidates=anvil.candidates.Select(e=>e.enhanced.id).ToArray();var current=anvil.localWeaponList.Select(e=>e.enhanced.id).ToArray();int offset=anvil.localRerollSeedOffset;
   var predicted=OtherDicePredictor.WeaponNext(anvil,Player);var again=OtherDicePredictor.WeaponNext(anvil,Player);
   if(!predicted.Select(e=>e.enhanced.id).SequenceEqual(again.Select(e=>e.enhanced.id))||!candidates.SequenceEqual(anvil.candidates.Select(e=>e.enhanced.id))||!current.SequenceEqual(anvil.localWeaponList.Select(e=>e.enhanced.id))||offset!=anvil.localRerollSeedOffset||Player.rerollDice!=10)throw new Exception("Weapon prediction mutated state");
   if(!preview.Weapons.Select(e=>e.enhanced.id).SequenceEqual(predicted.Select(e=>e.enhanced.id)))throw new Exception("Weapon displayed mismatch");
   Capture(weaponPanel,"dice-weapon-extra-choice.png");weaponPanel.Reroll();yield return new WaitForSecondsRealtime(.4f);
   if(!predicted.Select(e=>e.enhanced.id).SequenceEqual(anvil.localWeaponList.Select(e=>e.enhanced.id)))throw new Exception("Weapon actual mismatch");Log("PASS weapon "+i+" prediction read-only, displayed and actual="+String.Join(",",anvil.localWeaponList.Select(e=>e.enhanced.id.ToString()).ToArray()));
  }
  PersonalConveniencePlugin.Instance.ShowPreview.Value=false;yield return new WaitForSecondsRealtime(.2f);if(weaponPanel.GetComponent<OtherDicePreview>().PreviewRoot.activeInHierarchy)throw new Exception("Weapon off failed");PersonalConveniencePlugin.Instance.ShowPreview.Value=true;yield return new WaitForSecondsRealtime(.2f);weaponPanel.Close();Mirror.NetworkServer.Destroy(anvilObject);Player.AddCustomStatUnsafe("EXTRAWEAPONCHOICES",-1);
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








