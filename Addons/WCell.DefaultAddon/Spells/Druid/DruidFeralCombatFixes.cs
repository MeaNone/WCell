using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WCell.Constants;
using WCell.Constants.Items;
using WCell.Constants.Spells;
using WCell.Core.Initialization;
using WCell.RealmServer.Spells;

namespace WCell.Addons.Default.Spells.Druid
{
	public static class DruidFeralCombatFixes
	{
		[Initialization(InitializationPass.Second)]
		public static void FixIt()
		{
			// Thick Hide needs to be restricted to "cloth and leather items"
			SpellLineId.DruidFeralCombatThickHide.Apply(spell =>
			{
				spell.RequiredItemClass = ItemClass.Armor;
				spell.RequiredItemSubClassMask = ItemSubClassMask.ArmorCloth | ItemSubClassMask.ArmorLeather;
			});

			// Sharpened Claws only works "while in Bear, Dire Bear or Cat Form"
			SpellLineId.DruidFeralCombatSharpenedClaws.Apply(spell =>
			{
				spell.AllowedShapeshiftMask = ShapeShiftMask.Bear | ShapeShiftMask.DireBear | ShapeShiftMask.Cat;
			});
		}
	}
}