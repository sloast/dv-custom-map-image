using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

using UnityEngine;
using DV;
using static UnityModManagerNet.UnityModManager;
using System.IO;
using dnlib;
using DV.Teleporters;

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
	}

	private static void LoadTexture(bool force = false)
	{

		try
		{
			string imagePath = "";

			switch (settings.imageSelection)
			{
				case Settings.BuiltinMap.Default:
					imagePath = "";
					break;
				case Settings.BuiltinMap.GradeMap:
					imagePath = GetPath("GradeMap.png");
					break;
				case Settings.BuiltinMap.KotZoomiesMap:
					imagePath = GetPath("Kot_Derail_Valley_Zoomies_Map.png");
					break;
				case Settings.BuiltinMap.Custom:
					imagePath = settings.imagePath;
					break;
			}

			if (!force && imagePath == lastImagePath) return;
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
				mapTexture = null;
			}
		}
		catch (Exception ex)
		{
			mod?.Logger.LogException("Error loading image", ex);
			mapTexture = null;
		}
	}

	private static string GetPath(string relativePath)
	{
		return mod?.Path + relativePath;
	}
}

public class Settings : ModSettings, IDrawable
{
	[Header("Click save after changing the map image")]
	[Draw("Select map")] public BuiltinMap imageSelection = BuiltinMap.Default;
	[Draw("Image path (if \"Custom\" selected)")] public string imagePath = "";
	[Draw("Station names")] public bool showStationNames = true;
	[Draw("Station resource markers")] public bool showStationResources = true;
	[Draw("Legend")] public bool showLegend = true;

	public enum BuiltinMap
	{
		Default,
		GradeMap,
		KotZoomiesMap,
		Custom
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
	[HarmonyPatch("Start")]
	[HarmonyPostfix]
	public static void Start_Postfix(MapMarkersController __instance)
	{
		try
		{

			Transform paper = __instance.transform.Find("MapPaper");

			if (Main.mapTexture != null) {

				paper.Find("Map_LOD0").GetComponent<MeshRenderer>().material.mainTexture = Main.mapTexture;
				paper.Find("Map_LOD1").GetComponent<MeshRenderer>().material.mainTexture = Main.mapTexture;
			}

			if (!Main.settings.showLegend)
			{
				foreach(Transform child in paper)
				{
					if (child.name.StartsWith("Marker"))
					{
						child.gameObject.SetActive(false);
					}
				}
				paper.Find("Names/Legend").gameObject.SetActive(false);
			}

			if (!Main.settings.showStationNames)
			{
				foreach(Transform child in paper.Find("Names"))
				{
					if (child.name != "Legend")
					{
						child.gameObject.SetActive(false);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Main.mod?.Logger.LogException(ex);
		}
	}

	[HarmonyPatch("OnDestinationUpdated")]
	[HarmonyPostfix]
	public static void OnDestinationUpdated_Postfix(MapMarkersController __instance)
	{
		if (Main.settings.showStationResources) return;

		foreach (Transform child in __instance.transform)
		{
			if (child.name.StartsWith("MapSideMarker"))
			{
				child.gameObject.SetActive(false);
			}
		}
	}
}
