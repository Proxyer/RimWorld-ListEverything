﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public class MainTabWindow_List : MainTabWindow
	{
		public override Vector2 RequestedTabSize
		{
			get
			{
				return new Vector2(900, base.RequestedTabSize.y);
			}
		}

		public override void PreOpen()
		{
			base.PreOpen();
			RemakeList();
		}

		private Vector2 scrollPosition = Vector2.zero;

		private float scrollViewHeight;

		private float listHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			base.DoWindowContents(fillRect);
			Rect filterRect = fillRect.LeftPart(0.50f);
			Rect listRect = fillRect.RightPart(0.49f);
			listHeight = listRect.height;

			GUI.color = Color.grey;
			Widgets.DrawLineVertical(listRect.x-3, 0, listRect.height);
			GUI.color = Color.white;

			DoFilter(filterRect);
			DoList(listRect);
		}


		//Base Lists:
		enum BaseListType
		{
			All,
			Items,
			Everyone,
			Colonists,
			Animals,
			Buildings,
			Plants,
			Inventory,
			ThingRequestGroup,
			Haulables,
			Mergables,
			Filth
		};
		BaseListType[] normalTypes = 
			{ BaseListType.All, BaseListType.Items, BaseListType.Everyone, BaseListType.Colonists, BaseListType.Animals,
			BaseListType.Buildings, BaseListType.Plants, BaseListType.Inventory};
		BaseListType baseType = BaseListType.All;

		ThingRequestGroup listGroup = ThingRequestGroup.Everything;

		public void Reset()
		{
			baseType = BaseListType.All;
			filters = new List<ListFilter>() { new ListFilterName() };
			RemakeList();
		}
		List<Thing> listedThings;
		public static void RemakeListPlease() =>
			Find.WindowStack.WindowOfType<MainTabWindow_List>()?.RemakeList();
		public void RemakeList()
		{
			Map map = Find.CurrentMap;
			IEnumerable<Thing> allThings = Enumerable.Empty<Thing>();
			switch(baseType)
			{
				case BaseListType.All:
					allThings = ContentsUtility.AllKnownThings(map);
					break;
				case BaseListType.ThingRequestGroup:
					allThings = map.listerThings.ThingsInGroup(listGroup);
					break;
				case BaseListType.Buildings:
					allThings = map.listerBuildings.allBuildingsColonist.Cast<Thing>();
					break;
				case BaseListType.Plants:
					allThings = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);
					break;
				case BaseListType.Inventory:
					List<IThingHolder> holders = new List<IThingHolder>();
					map.GetChildHolders(holders);
					List<Thing> list = new List<Thing>();
					foreach (IThingHolder holder in holders.Where(ContentsUtility.CanPeekInventory))
						list.AddRange(ContentsUtility.AllKnownThings(holder));
					allThings = list;
					break;
				case BaseListType.Items:
					allThings = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways);
					break;
				case BaseListType.Everyone:
					allThings = map.mapPawns.AllPawnsSpawned.Cast<Thing>();
					break;
				case BaseListType.Colonists:
					allThings = map.mapPawns.FreeColonistsSpawned.Cast<Thing>();
					break;
				case BaseListType.Animals:
					allThings = map.mapPawns.AllPawnsSpawned.Where(p => !p.RaceProps.Humanlike).Cast<Thing>();
					break;
				case BaseListType.Haulables:
					allThings = map.listerHaulables.ThingsPotentiallyNeedingHauling();
					break;
				case BaseListType.Mergables:
					allThings = map.listerMergeables.ThingsPotentiallyNeedingMerging();
					break;
				case BaseListType.Filth:
					allThings = map.listerFilthInHomeArea.FilthInHomeArea;
					break;
			}

			//Filters
			allThings = allThings.Where(t => !(t.ParentHolder is Corpse) && !(t.ParentHolder is MinifiedThing));
			if (!DebugSettings.godMode)
			{
				allThings = allThings.Where(t => t.def.drawerType != DrawerType.None);//Probably a good filter
				allThings = allThings.Where(t => !t.PositionHeld.Fogged(map));
			}
			foreach(ListFilter filter in filters)
				allThings = filter.Apply(allThings);

			//Sort
			listedThings = allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000).ToList();
		}

		public string BaseTypeDesc()
		{
			switch (baseType)
			{
				case BaseListType.ThingRequestGroup:
					return "ThingRequestGroup";
				case BaseListType.Buildings:
					return "Colonist buildings";
				case BaseListType.Haulables:
					return "Things to be hauled";
				case BaseListType.Mergables:
					return "Stacks to be merged";
				case BaseListType.Filth:
					return "Filth in home area";
			}
			return baseType.ToString();
		}

		public void DoListingBase(Listing_Standard listing)
		{
			switch (baseType)
			{
				case BaseListType.ThingRequestGroup:
					if (listing.ButtonTextLabeled("Group:", listGroup.ToString()))
					{
						List<FloatMenuOption> groups = new List<FloatMenuOption>();
						foreach (ThingRequestGroup type in Enum.GetValues(typeof(ThingRequestGroup)))
							groups.Add(new FloatMenuOption(type.ToString(), () => listGroup = type));

						FloatMenu floatMenu = new FloatMenu(groups) { onCloseCallback = RemakeList };
						floatMenu.vanishIfMouseDistant = true;
						Find.WindowStack.Add(floatMenu);
					}
					break;
			}
		}

		//Filters:
		public List<ListFilter> filters = new List<ListFilter>() { new ListFilterName() };
		public void DoFilter(Rect rect)
		{
			Text.Font = GameFont.Medium;
			Rect headerRect = rect.TopPartPixels(Text.LineHeight);
			Rect filterRect = rect.BottomPartPixels(rect.height - Text.LineHeight);

			//Header
			Rect refreshRect = headerRect.RightPartPixels(Text.LineHeight).ContractedBy(2f);
			Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - Text.LineHeight, headerRect.height);

			if (Widgets.ButtonImage(refreshRect, TexUI.RotRightTex))
				RemakeList();
			TooltipHandler.TipRegion(refreshRect, "The list is saved when filter is changed - new items aren't added until refreshed");

			Widgets.Label(labelRect, $"Listing: {BaseTypeDesc()}");
			Widgets.DrawHighlightIfMouseover(labelRect);
			if (Widgets.ButtonInvisible(labelRect))
			{
				List<FloatMenuOption> types = new List<FloatMenuOption>();
				foreach (BaseListType type in Enum.GetValues(typeof(BaseListType)))
				{
					if(Prefs.DevMode || normalTypes.Contains(type))
						types.Add(new FloatMenuOption(type.ToString(), () => baseType = type));
				}

				FloatMenu floatMenu = new FloatMenu(types) { onCloseCallback = RemakeList };
				floatMenu.vanishIfMouseDistant = true;
				Find.WindowStack.Add(floatMenu);
			}

			Listing_Standard listing = new Listing_Standard();
			listing.Begin(filterRect);

			//List base
			DoListingBase(listing);

			//Filters
			listing.GapLine();
			if (DoFilters(listing, filters))
				RemakeList();

			listing.GapLine();

			//Bottom Buttons
			Rect buttonRect = listing.GetRect(Text.LineHeight);
			buttonRect = buttonRect.LeftPart(0.5f);
			
			if (Widgets.ButtonText(buttonRect, "Add"))
				AddFilterFloat(filters);

			buttonRect.x += buttonRect.width;
			if (Widgets.ButtonText(buttonRect, "Reset All"))
				Reset();

			//Global Options
			listing.CheckboxLabeled(
				"Restrict Filter Options To Available Things",
				ref ContentsUtility.onlyAvailable,
				"For example, don't show the option 'Made from Plasteel' if nothing is made form plasteel");

			listing.End();
		}

		public static bool DoFilters(Listing_Standard listing, List<ListFilter> filters)
		{
			bool changed = false;
			foreach (ListFilter filter in filters)
				changed |= filter.Listing(listing);

			filters.RemoveAll(f => f.delete);
			return changed;
		}

		public static void AddFilterFloat(List<ListFilter> filters, params ListFilterDef[] exclude)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterDef def in DefDatabase<ListFilterDef>.AllDefs.Where(d => !exclude.Contains(d) && (Prefs.DevMode || !d.devOnly)))
				options.Add(new FloatMenuOption(def.LabelCap, () => filters.Add(ListFilterMaker.MakeFilter(def))));
			FloatMenu floatMenu = new FloatMenu(options) { onCloseCallback = RemakeListPlease };
			floatMenu.vanishIfMouseDistant = true;
			Find.WindowStack.Add(floatMenu);
		}


		public void DoList(Rect listRect)
		{
			//Handle mouse selection
			if (!Input.GetMouseButton(0))
			{
				dragSelect = false;
				dragDeselect = false;
			}
			if (!Input.GetMouseButton(1))
				dragJump = false;

			selectAllDef = null;

			Map map = Find.CurrentMap;
			
			//Draw Scrolling List:
			Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
			Rect thingRect = new Rect(viewRect.x, 0, viewRect.width, 32);

			foreach (Thing thing in listedThings)
			{
				//Be smart about drawing only what's shown.
				if (thingRect.y + 32 >= scrollPosition.y)
					DrawThingRow(thing, ref thingRect);

				thingRect.y += 34;

				if (thingRect.y > scrollPosition.y + listHeight)
					break;
			}

			if (Event.current.type == EventType.Layout)
				scrollViewHeight = listedThings.Count() * 34f;

			//Select all for double-click
			if(selectAllDef != null)
			{
				foreach(Thing t in listedThings)
				{
					if (t.def == selectAllDef)
						TrySelect.Select(t, false);
				}
			}

			Widgets.EndScrollView();
		}

		bool dragSelect = false;
		bool dragDeselect = false;
		bool dragJump = false;
		ThingDef selectAllDef;
		private void DrawThingRow(Thing thing, ref Rect rect)
		{
			//Highlight selected
			if (Find.Selector.IsSelected(thing))
				Widgets.DrawHighlightSelected(rect);

			//Draw
			DrawThing(rect, thing);

			//Draw arrow pointing to hovered thing
			if (Mouse.IsOver(rect))
			{
				Vector3 center = UI.UIToMapPosition((float)(UI.screenWidth / 2), (float)(UI.screenHeight / 2));
				bool arrow = (center - thing.DrawPos).MagnitudeHorizontalSquared() >= 121f;//Normal arrow is 9^2, using 11^1 seems good too.
				TargetHighlighter.Highlight(thing, arrow, true, true);
			}

			//Mouse event: select.
			if (Mouse.IsOver(rect))
			{
				if (Event.current.type == EventType.mouseDown)
				{
					if (!thing.def.selectable || !thing.Spawned)
					{
						CameraJumper.TryJump(thing);
						if (Event.current.alt)
							Find.MainTabsRoot.EscapeCurrentTab(false);
					}
					else if (Event.current.clickCount == 2 && Event.current.button == 0)
					{
						selectAllDef = thing.def;
					}
					else if (Event.current.shift)
					{
						if (Find.Selector.IsSelected(thing))
						{
							dragDeselect = true;
							Find.Selector.Deselect(thing);
						}
						else
						{
							dragSelect = true;
							TrySelect.Select(thing);
						}
					}
					else if (Event.current.alt)
					{
						Find.MainTabsRoot.EscapeCurrentTab(false);
						CameraJumper.TryJumpAndSelect(thing);
					}
					else
					{
						if (Event.current.button == 1)
						{
							CameraJumper.TryJump(thing);
							dragJump = true;
						}
						else if (Find.Selector.IsSelected(thing))
						{
							CameraJumper.TryJump(thing);
							dragSelect = true;
						}
						else
						{
							Find.Selector.ClearSelection();
							TrySelect.Select(thing);
							dragSelect = true;
						}
					}
				}
				if (Event.current.type == EventType.mouseDrag)
				{
					if (!thing.def.selectable || !thing.Spawned)
						CameraJumper.TryJump(thing);
					else if (dragJump)
						CameraJumper.TryJump(thing);
					else if (dragSelect)
						TrySelect.Select(thing, false);
					else if (dragDeselect)
						Find.Selector.Deselect(thing);
				}
			}
		}

		public static void DrawThing(Rect rect, Thing thing)
		{
			//Label
			Widgets.Label(rect, thing.LabelCap);

			ThingDef def = thing.def.entityDefToBuild as ThingDef ?? thing.def;
			Rect iconRect = rect.RightPartPixels(32 * (def.graphicData?.drawSize.x / def.graphicData?.drawSize.y ?? 1f));
			//Icon
			if (thing is Frame frame)
			{
				Widgets.ThingIcon(iconRect, def);
			}
			else if (def.graphic is Graphic_Linked && def.uiIconPath.NullOrEmpty())
			{
				Material iconMat = def.graphic.MatSingle;
				Rect texCoords = new Rect(iconMat.mainTextureOffset, iconMat.mainTextureScale);
				GUI.color = thing.DrawColor;
				Widgets.DrawTextureFitted(iconRect, def.uiIcon, 1f, Vector2.one, texCoords);
				GUI.color = Color.white;
			}
			else
			{
				if (thing.Graphic is Graphic_Cluster)
					Rand.PushState();
				Widgets.ThingIcon(iconRect, thing);
				if (thing.Graphic is Graphic_Cluster)
					Rand.PopState();
			}
		}
	}
}
