﻿/* Copyright (c) 2019 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Gibbed.Unreflect.Core;
using Newtonsoft.Json;
using Dataminer = BorderlandsOzDatamining.Dataminer;

namespace DumpBalance
{
    internal class Program
    {
        public static readonly MultiSetComparer<string> StringComparer;

        static Program()
        {
            StringComparer = new MultiSetComparer<string>();
        }

        private static void Main(string[] args)
        {
            new Dataminer().Run(args, Go);
        }

        private static void Go(Engine engine)
        {
            var weaponBalanceClass = engine.GetClass("WillowGame.WeaponBalanceDefinition");
            var missionWeaponBalanceClass = engine.GetClass("WillowGame.MissionWeaponBalanceDefinition");
            var inventoryBalanceClass = engine.GetClass("WillowGame.InventoryBalanceDefinition");
            var itemBalanceClass = engine.GetClass("WillowGame.ItemBalanceDefinition");
            var classModBalanceClass = engine.GetClass("WillowGame.ClassModBalanceDefinition");
            var itemClass = engine.GetClass("WillowGame.ItemDefinition");
            if (weaponBalanceClass == null ||
                missionWeaponBalanceClass == null ||
                inventoryBalanceClass == null ||
                itemBalanceClass == null ||
                classModBalanceClass == null ||
                itemClass == null)
            {
                throw new InvalidOperationException();
            }

            using (var writer = Dataminer.NewDump("Weapon Balance.json"))
            {
                writer.WriteStartObject();

                var balances = engine.Objects
                    .Where(o => (o.IsA(inventoryBalanceClass) ||
                                 o.IsA(weaponBalanceClass)) &&
                                o.GetName().StartsWith("Default__") == false)
                    .OrderBy(o => o.GetPath());
                foreach (dynamic balance in balances)
                {
                    var type = GetBalanceType(balance);
                    if (type == null)
                    {
                        throw new InvalidOperationException();
                    }

                    var typeClassPath = type.GetClass().Path;
                    if (typeClassPath == "WillowGame.UsableItemDefinition" ||
                        typeClassPath == "WillowGame.ArtifactDefinition" ||
                        typeClassPath == "WillowGame.UsableCustomizationItemDefinition" ||
                        typeClassPath == "WillowGame.ClassModDefinition" ||
                        typeClassPath == "WillowGame.GrenadeModDefinition" ||
                        typeClassPath == "WillowGame.ShieldDefinition" ||
                        typeClassPath == "WillowGame.MissionItemDefinition" ||
                        typeClassPath == "WillowGame.CrossDLCClassModDefinition")
                    {
                        continue;
                    }

                    if (typeClassPath != "WillowGame.WeaponTypeDefinition")
                    {
                        throw new InvalidOperationException();
                    }

                    var balancePath = (string)balance.GetPath();

                    writer.WritePropertyName(balancePath);
                    writer.WriteStartObject();

                    var baseDefinition = balance.BaseDefinition;
                    if (baseDefinition != null)
                    {
                        writer.WritePropertyName("base");
                        writer.WriteValue(baseDefinition.GetPath());
                    }

                    var typePath = (string)balance.InventoryDefinition?.GetPath();
                    var baseTypePath = (string)balance.BaseDefinition?.InventoryDefinition?.GetPath();
                    if (typePath != null && (baseTypePath == null || typePath != baseTypePath))
                    {
                        writer.WritePropertyName("type");
                        writer.WriteValue(typePath);
                    }

                    var manufacturers = balance.Manufacturers;
                    if (manufacturers != null && manufacturers.Length > 0)
                    {
                        writer.WritePropertyName("manufacturers");
                        writer.WriteStartArray();
                        foreach (var manufacturer in ((IEnumerable<dynamic>)manufacturers)
                            .Where(imbd => imbd.Manufacturer != null)
                            .OrderBy(imbd => imbd.Manufacturer.GetPath()))
                        {
                            writer.WriteValue(manufacturer.Manufacturer.GetPath());
                        }
                        writer.WriteEndArray();
                    }

                    if (balance.PartListCollection != null)
                    {
                        throw new NotSupportedException();
                    }

                    if (balance.GetClass() == weaponBalanceClass)
                    {
                        var partList = balance.RuntimePartListCollection;
                        if (partList == null)
                        {
                            throw new InvalidOperationException();
                        }

                        if (partList != null)
                        {
                            var basePartList = baseDefinition == null ? null : baseDefinition.RuntimePartListCollection;

                            PartReplacementMode? mode = null;
                            var bodyPartData = BuildCustomPartTypeData(partList.BodyPartData, basePartList?.BodyPartData, ref mode);
                            var gripPartData = BuildCustomPartTypeData(partList.GripPartData, basePartList?.GripPartData, ref mode);
                            var barrelPartData = BuildCustomPartTypeData(partList.BarrelPartData, basePartList?.BarrelPartData, ref mode);
                            var sightPartData = BuildCustomPartTypeData(partList.SightPartData, basePartList?.SightPartData, ref mode);
                            var stockPartData = BuildCustomPartTypeData(partList.StockPartData, basePartList?.StockPartData, ref mode);
                            var elementalPartData = BuildCustomPartTypeData(partList.ElementalPartData, basePartList?.ElementalPartData, ref mode);
                            var accessory1PartData = BuildCustomPartTypeData(partList.Accessory1PartData, basePartList?.Accessory1PartData, ref mode);
                            var accessory2PartData = BuildCustomPartTypeData(partList.Accessory2PartData, basePartList?.Accessory2PartData, ref mode);
                            var materialPartData = BuildCustomPartTypeData(partList.MaterialPartData, basePartList?.MaterialPartData, ref mode);

                            if (mode == null)
                            {
                                throw new InvalidOperationException();
                            }

                            writer.WritePropertyName("parts");
                            writer.WriteStartObject();

                            writer.WritePropertyName("mode");
                            writer.WriteValue(mode.ToString());

                            var associatedWeaponTypePath = (string)partList.AssociatedWeaponType?.GetPath();
                            var baseAssociatedWeaponTypePath = (string)basePartList?.AssociatedWeaponType?.GetPath();
                            if (associatedWeaponTypePath != null &&
                                (baseAssociatedWeaponTypePath == null || associatedWeaponTypePath != baseAssociatedWeaponTypePath))
                            {
                                writer.WritePropertyName("type");
                                writer.WriteValue(associatedWeaponTypePath);
                            }

                            WriteStrings(writer, "body", bodyPartData);
                            WriteStrings(writer, "grip", gripPartData);
                            WriteStrings(writer, "barrel", barrelPartData);
                            WriteStrings(writer, "sight", sightPartData);
                            WriteStrings(writer, "stock", stockPartData);
                            WriteStrings(writer, "elemental", elementalPartData);
                            WriteStrings(writer, "accessory1", accessory1PartData);
                            WriteStrings(writer, "accessory2", accessory2PartData);
                            WriteStrings(writer, "material", materialPartData);

                            writer.WriteEndObject();
                        }
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            using (var writer = Dataminer.NewDump("Item Balance.json"))
            {
                writer.WriteStartObject();

                var balances = engine.Objects
                    .Where(o => (o.IsA(inventoryBalanceClass) == true ||
                                 o.IsA(itemBalanceClass) == true ||
                                 o.IsA(classModBalanceClass) == true) &&
                                o.GetName().StartsWith("Default__") == false)
                    .OrderBy(o => o.GetPath());
                foreach (dynamic balance in balances)
                {
                    if (balance.GetClass() == inventoryBalanceClass)
                    {
                        var type = GetBalanceType(balance);
                        if (type == null)
                        {
                            throw new InvalidOperationException();
                        }

                        var typeClassPath = type.GetClass().Path;
                        if (typeClassPath == "WillowGame.WeaponTypeDefinition")
                        {
                            continue;
                        }
                    }

                    var balanceClass = balance.GetClass();
                    var balancePath = (string)balance.GetPath();

                    writer.WritePropertyName(balancePath);
                    writer.WriteStartObject();

                    var baseBalance = balance.BaseDefinition;
                    if (baseBalance != null)
                    {
                        writer.WritePropertyName("base");
                        writer.WriteValue(baseBalance.GetPath());
                    }

                    var typePath = (string)balance.InventoryDefinition?.GetPath();
                    var baseTypePath = (string)balance.BaseDefinition?.InventoryDefinition?.GetPath();
                    if (typePath != null && (baseTypePath == null || typePath != baseTypePath))
                    {
                        writer.WritePropertyName("type");
                        writer.WriteValue(typePath);
                    }

                    if (balanceClass == classModBalanceClass &&
                        balance.ClassModDefinitions.Length > 0)
                    {
                        dynamic[] classModDefinitions = balance.ClassModDefinitions;

                        writer.WritePropertyName("types");
                        writer.WriteStartArray();
                        foreach (var classModDefinition in classModDefinitions.OrderBy(cmd => cmd.GetPath()))
                        {
                            writer.WriteValue(classModDefinition.GetPath());
                        }
                        writer.WriteEndArray();
                    }

                    var manufacturers = balance.Manufacturers;
                    if (manufacturers != null &&
                        manufacturers.Length > 0)
                    {
                        writer.WritePropertyName("manufacturers");
                        writer.WriteStartArray();
                        foreach (var manufacturer in ((IEnumerable<dynamic>)manufacturers)
                            .Where(imbd => imbd.Manufacturer != null)
                            .OrderBy(imbd => imbd.Manufacturer.GetPath()))
                        {
                            writer.WriteValue(manufacturer.Manufacturer.GetPath());
                        }
                        writer.WriteEndArray();
                    }

                    var partList = balanceClass == inventoryBalanceClass
                        ? balance.PartListCollection : balance.RuntimePartListCollection;
                    if (partList != null)
                    {
                        if (partList.GetClass().Path != "WillowGame.ItemPartListCollectionDefinition")
                        {
                            throw new InvalidOperationException();
                        }

                        var basePartList = baseBalance == null || baseBalance.GetClass() == inventoryBalanceClass
                            ? null : baseBalance.RuntimePartListCollection;

                        PartReplacementMode? mode = null;
                        var alphaPartData = BuildCustomPartTypeData(partList.AlphaPartData, basePartList?.AlphaPartData, ref mode);
                        var betaPartData = BuildCustomPartTypeData(partList.BetaPartData, basePartList?.BetaPartData, ref mode);
                        var gammaPartData = BuildCustomPartTypeData(partList.GammaPartData, basePartList?.GammaPartData, ref mode);
                        var deltaPartData = BuildCustomPartTypeData(partList.DeltaPartData, basePartList?.DeltaPartData, ref mode);
                        var epsilonPartData = BuildCustomPartTypeData(partList.EpsilonPartData, basePartList?.EpsilonPartData, ref mode);
                        var zetaPartData = BuildCustomPartTypeData(partList.ZetaPartData, basePartList?.ZetaPartData, ref mode);
                        var etaPartData = BuildCustomPartTypeData(partList.EtaPartData, basePartList?.EtaPartData, ref mode);
                        var thetaPartData = BuildCustomPartTypeData(partList.ThetaPartData, basePartList?.ThetaPartData, ref mode);
                        var materialPartData = BuildCustomPartTypeData(partList.MaterialPartData, basePartList?.MaterialPartData, ref mode);

                        if (mode == null)
                        {
                            mode = PartReplacementMode.Additive;
                        }

                        writer.WritePropertyName("parts");
                        writer.WriteStartObject();

                        writer.WritePropertyName("mode");
                        writer.WriteValue(mode.ToString());

                        var associatedItemPath = (string)partList.AssociatedItem?.GetPath();
                        var baseAssociatedItemPath = (string)basePartList?.AssociatedItem?.GetPath();
                        if (associatedItemPath != null &&
                            (baseAssociatedItemPath == null || associatedItemPath != baseAssociatedItemPath))
                        {
                            writer.WritePropertyName("type");
                            writer.WriteValue(associatedItemPath);
                        }

                        WriteStrings(writer, "alpha", alphaPartData);
                        WriteStrings(writer, "beta", betaPartData);
                        WriteStrings(writer, "gamma", gammaPartData);
                        WriteStrings(writer, "delta", deltaPartData);
                        WriteStrings(writer, "epsilon", epsilonPartData);
                        WriteStrings(writer, "zeta", zetaPartData);
                        WriteStrings(writer, "eta", etaPartData);
                        WriteStrings(writer, "theta", thetaPartData);
                        WriteStrings(writer, "material", materialPartData);

                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }
        }

        private static dynamic GetBalanceType(dynamic balance)
        {
            while (balance != null)
            {
                if (balance.InventoryDefinition != null)
                {
                    return balance.InventoryDefinition;
                }
                balance = balance.BaseDefinition;
            }
            return null;
        }

        private static List<string> BuildCustomPartTypeData(dynamic data, dynamic baseData, ref PartReplacementMode? mode)
        {
            if ((bool)data.bEnabled == false)
            {
                return null;
            }

            var partPaths = new List<string>();
            foreach (var weightedPart in (dynamic[])data.WeightedParts)
            {
                if (weightedPart.Part == null)
                {
                    partPaths.Add(null);
                }
                else
                {
                    partPaths.Add(weightedPart.Part.GetPath());
                }
            }

            if (baseData == null)
            {
                if (mode == null)
                {
                    mode = PartReplacementMode.Additive;
                }

                return partPaths;
            }

            if ((bool)baseData.bEnabled == false)
            {
                if (mode == null)
                {
                    mode = PartReplacementMode.Additive;
                }

                return partPaths;
            }

            var basePartPaths = new List<string>();
            foreach (var weightedPart in (dynamic[])baseData.WeightedParts)
            {
                if (weightedPart.Part == null)
                {
                    basePartPaths.Add(null);
                }
                else
                {
                    basePartPaths.Add(weightedPart.Part.GetPath());
                }
            }

            if (mode == PartReplacementMode.Selective || basePartPaths.Except(partPaths).Any() == true)
            {
                mode = PartReplacementMode.Selective;
                return StringComparer.Equals(partPaths, basePartPaths) == true ? null : partPaths;
            }

            if (mode != null && mode != PartReplacementMode.Additive)
            {
                throw new InvalidOperationException();
            }

            if (mode == null)
            {
                mode = PartReplacementMode.Additive;
            }

            partPaths = partPaths.Except(basePartPaths).ToList();
            return partPaths.Count == 0 ? null : partPaths;
        }

        private static void WriteStrings(JsonWriter writer, string name, IEnumerable<string> enumerable)
        {
            if (enumerable == null)
            {
                return;
            }

            writer.WritePropertyName(name);
            writer.WriteStartArray();
            foreach (var value in enumerable)
            {
                writer.WriteValue(value);
            }
            writer.WriteEndArray();
        }
    }
}
