using System;
using System.Collections;
using System.IO;
using System.Reflection;
using dnlib;
using DV;
using DV.Teleporters;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;
using static UnityModManagerNet.UnityModManager.Param;

namespace custom_map_image;

#if DEBUG
[EnableReloading]
#endif
public static class Main
{
	private static Harmony? harmony;
	public static ModEntry? mod;
	public static Texture2D? mapTexture = null;
	public static Settings settings = new();
	public static bool settingsChanged = false;

	public static string lastImagePath = "";

	public static bool Load(UnityModManager.ModEntry modEntry)
	{
		try
		{
			mod = modEntry;
			modEntry.OnUnload = Unload;
			settings = Settings.Load<Settings>(modEntry);

			modEntry.OnGUI = OnGUI;
			modEntry.OnSaveGUI = OnSaveGUI;

			harmony = new Harmony(modEntry.Info.Id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			LoadTexture(force: true);
		}
		catch (Exception ex)
		{
			modEntry.Logger.LogException($"Failed to load {modEntry.Info.DisplayName}:", ex);
			harmony?.UnpatchAll(modEntry.Info.Id);
			return false;
		}

		return true;
	}

	public static bool Unload(UnityModManager.ModEntry modEntry)
	{
		harmony?.UnpatchAll(mod?.Info.Id);

		return true;
	}

	static void OnGUI(UnityModManager.ModEntry modEntry)
	{
		settings?.Draw(modEntry);
	}

	static void OnSaveGUI(UnityModManager.ModEntry modEntry)
	{
		settings?.Save(modEntry);
		LoadTexture();
		settingsChanged = true;
	}

	public static void LoadTexture(bool force = false)
	{
		try
		{
			string imagePath = settings.GetImagePath();

			if (!force && imagePath == lastImagePath)
				return;
			lastImagePath = imagePath;

			if (imagePath != "")
			{
				Texture2D tex = new(2, 2);
				byte[] data = File.ReadAllBytes(imagePath);
				tex.LoadImage(data);
				mapTexture = tex;
			}
			else
			{
				mapTexture = (Texture2D)Resources.Load("WorldMap");
			}
		}
		catch (Exception ex)
		{
			mod?.Logger.LogException("Error loading image", ex);
			mapTexture = null;
		}
	}
}

public class Settings : ModSettings, IDrawable
{
	[Header("Click save after changing settings")]
	[Draw("Select map")]
	public BuiltinMap imageSelection = BuiltinMap.GradeMap;

	[Draw("Image path (if \"Custom\" selected)")]
	public string imagePath = "";

	[Draw("Station names")]
	public bool showStationNames = true;

	[Draw("Station resource markers")]
	public bool showStationResources = true;

	[Draw("Legend")]
	public bool showLegend = true;

	public enum BuiltinMap
	{
		Default,
		GradeMap,
		KotZoomiesMap,
		HeightAndSpeed,
		Custom,
	}

	public string GetImagePath() {
		switch (imageSelection)
		{
			case BuiltinMap.Default:
				return "";
			case BuiltinMap.GradeMap:
				return GetPath("GradeMap.png");
			case BuiltinMap.KotZoomiesMap:
				return GetPath("Kot_Derail_Valley_Zoomies_Map.png");
			case BuiltinMap.HeightAndSpeed:
				return GetPath("YNakajima_height_and_speed_map.png");
			case BuiltinMap.Custom:
				return imagePath;
		}
		return "";
	}

	private static string GetPath(string relativePath)
	{
		return Main.mod?.Path + relativePath;
	}

	public override void Save(UnityModManager.ModEntry modEntry)
	{
		Save(this, modEntry);
	}

	public void OnChange() { }
}

[HarmonyPatch(typeof(MapMarkersController))]
public class MapMarkersController_Patch
{
	private static Texture defaultMapTex = null!;

	private static void UpdateMapTexture(MapMarkersController __instance, bool onlyMarkers = false)
	{
		try
		{
			foreach (Transform child in __instance.transform)
			{
				if (child.name.StartsWith("MapSideMarker"))
				{
					child.gameObject.SetActive(Main.settings.showStationResources);
				}
			}

			if (onlyMarkers) return;

			Transform paper = __instance.transform.Find("MapPaper");

			if (defaultMapTex is null)
				defaultMapTex = __instance.transform.Find("MapPaper/Map_LOD0").GetComponent<MeshRenderer>().material.mainTexture;

			Texture texture = Main.mapTexture ?? defaultMapTex;

			paper.Find("Map_LOD0").GetComponent<MeshRenderer>().material.mainTexture = texture;
			paper.Find("Map_LOD1").GetComponent<MeshRenderer>().material.mainTexture = texture;
			

			foreach (Transform child in paper)
			{
				if (child.name.StartsWith("Marker"))
				{
					child.gameObject.SetActive(Main.settings.showLegend);
				}
			}
			paper.Find("Names/Legend").gameObject.SetActive(Main.settings.showLegend);

			foreach (Transform child in paper.Find("Names"))
			{
				if (child.name != "Legend")
				{
					child.gameObject.SetActive(Main.settings.showStationNames);
				}
			}
		}
		catch (Exception ex)
		{
			Main.mod?.Logger.LogException(ex);
		}
	}

	[HarmonyPatch("Start")]
	[HarmonyPostfix]
	private static void Start_Postfix(MapMarkersController __instance)
	{
		UpdateMapTexture(__instance);

		//__instance.StartCoroutine(UpdateSettingsCoro(__instance));
	}

	//private static IEnumerator UpdateSettingsCoro(MapMarkersController __instance)
	//{
	//	for (;;)
	//	{
	//		if (Main.settingsChanged)
	//		{
	//			UpdateMapTexture(__instance);
	//			Main.settingsChanged = false;
	//		}
	//		yield return new WaitForSeconds(1f);
	//	}
	//}

	[HarmonyPatch("OnDestinationUpdated")]
	[HarmonyPostfix]
	private static void OnDestinationUpdated_Postfix(MapMarkersController __instance)
	{
		UpdateMapTexture(__instance, onlyMarkers: true);
	}
}
