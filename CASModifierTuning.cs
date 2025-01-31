﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using System.Globalization;
using s4pi.Package;
using s4pi.Interfaces;

namespace TS4SimRipper
{
    public class CASModifierTuning
    {
        private const string CASModifierTuningPackage = "CASModifierTuning.package";

        private List<Tuning> tuning;
        private List<Conversion> conversions;
        //private List<Dampening> dampening;

        public Dictionary<ulong, float> CASModifierScales(Species species, SimOccult occult, AgeGender age, AgeGender gender)
        {
            foreach (Tuning t in this.tuning)
            {
                if (t.species == species && t.occult == occult && t.age == age && (t.gender & gender) > 0) return t.weights;
            }
            return null;
        }

        public Dictionary<ulong, string> DmapConversions(Species species, SimOccult occult, AgeGender age, AgeGender gender, 
            BodyType partType, AgeGender partGender)
        {
            foreach (Conversion c in this.conversions)
            {
                if (c.species == species && (c.occult & occult) > 0 && (c.age & age) > 0 && (c.gender & gender) > 0 && 
                    (c.partBodyType & partType) > 0 && c.partGender == partGender)
                    return c.dmapConversion;
            }
            return null;
        }

        //public Dictionary<ulong, float> CASModifierOffsets(Species species, SimOccult occult, AgeGender age, AgeGender gender)
        //{
        //    foreach (Tuning t in this.tuning)
        //    {
        //        if (t.species == species && t.occult == occult && t.age == age && (t.gender & gender) > 0) return t.offsets;
        //    }
        //    return null;
        //}

        public Dictionary<ulong, string> CASModifierNames(Species species, SimOccult occult, AgeGender age, AgeGender gender)
        {
            foreach (Tuning t in this.tuning)
            {
                if (t.species == species && t.occult == occult && t.age == age && (t.gender & gender) > 0) return t.names;
            }
            return null;
        }

        //public List<ulong> SculptDampening(Species species, SimOccult occult, AgeGender age, AgeGender gender, out List<Dictionary<ulong, float>> sculptWeights)
        //{
        //    foreach (Dampening d in this.dampening)
        //    {
        //        if (d.species == species && d.occult == occult && (d.age & age) > 0 && (d.gender & gender) > 0)
        //        {
        //            sculptWeights = d.sculptLimits;
        //            return d.modifiers;
        //        }
        //    }
        //    sculptWeights = new List<Dictionary<ulong, float>>();
        //    return new List<ulong>();
        //}

        public CASModifierTuning(Package[] packages, string[] packageNames, Form1 form, Dictionary<(uint, uint, ulong), (string, Package)> allKeys, Dictionary<(uint, uint, ulong), (string, Package)> allCCKeys)
        {
            this.tuning = new List<Tuning>();


            this.conversions = new List<Conversion>();
            //this.dampening = new List<Dampening>();
            string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(executingPath, CASModifierTuningPackage);
            if (!File.Exists(resourcePath))
            {
                throw new ApplicationException("Can't load CASModifierTuning");
            }
            Package p = (Package)Package.OpenPackage(1, resourcePath, false);
            List<IResourceIndexEntry> tuning = p.GetResourceList;
            int tuningCount = 0;
            foreach (IResourceIndexEntry ires in tuning)
            {
                tuningCount += 1;
                if (ires.ResourceType != 0xF3ABFF3C) continue;
                bool foundCC = false;

                (uint ResourceType, uint ResourceGroup, ulong Instance) key = (ires.ResourceType, ires.ResourceGroup, ires.Instance);
                (uint ResourceType, uint ResourceGroup, ulong Instance) alternativeKey = (0x03B33DDF, ires.ResourceGroup, ires.Instance);

                if (allCCKeys.ContainsKey(key))
                {
                    Predicate<IResourceIndexEntry> pred = r => (r.ResourceType == ires.ResourceType || r.ResourceType == 0x03B33DDF) &
                                            r.Instance == ires.Instance;
                    Package ccPackage = allCCKeys[key].Item2;
                    IResourceIndexEntry iresCC = ccPackage.Find(pred);
                    if (iresCC != null)
                        foundCC = ReadConversions(packages, form, foundCC, ccPackage, iresCC);
                }

                else if (allCCKeys.ContainsKey(alternativeKey))
                    {
                        Predicate<IResourceIndexEntry> pred = r => (r.ResourceType == ires.ResourceType || r.ResourceType == 0x03B33DDF) &
                                                r.Instance == ires.Instance;
                        Package ccPackage = allCCKeys[alternativeKey].Item2;
                        IResourceIndexEntry iresCC = ccPackage.Find(pred);
                        if (iresCC != null)
                            foundCC = ReadConversions(packages, form, foundCC, ccPackage, iresCC);
                    }
                


                ReadCASModifiers(ref p, ires, foundCC);
            }

        }

        private bool ReadConversions(Package[] gamePackages, Form1 form, bool foundCC, Package package, IResourceIndexEntry iresCC)
        {
            {
                Stream t = package.GetResource(iresCC);
                Stream s = new MemoryStream();
                t.CopyTo(s);
                s.Position = 0;
                form.TroubleshootPackageTuning.AddResource(iresCC, s, true);
                t.Position = 0;

                XmlTextReader reader = new XmlTextReader(t);
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string str = reader.GetAttribute("c");
                        if (str != null && String.Compare(str, "Client_CASModifierTuning") == 0)
                        {
                            this.tuning.Add(ParseTuning(reader));
                            foundCC = true;
                            break;
                        }
                        else if (str != null && String.Compare(str, "Client_CASModifierPartGenderDmaps") == 0)
                        {
                            this.conversions.Add(ParseConversion(reader));
                            foundCC = true;
                            break;
                        }
                        //else if (str != null && String.Compare(str, "Client_CASModifierDampening") == 0)
                        //{
                        //    this.dampening.Add(ParseDampening(reader));
                        //    foundCC = true;
                        //    break;
                        //}
                    }
                }
            }

            return foundCC;
        }

        private void ReadCASModifiers(ref Package p, IResourceIndexEntry ires, bool foundCC)
        {
            if (!foundCC)
            {
                XmlTextReader reader = new XmlTextReader(p.GetResource(ires));
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string str = reader.GetAttribute("c");
                        if (str != null && String.Compare(str, "Client_CASModifierTuning") == 0)
                        {
                            this.tuning.Add(ParseTuning(reader));
                        }
                        else if (str != null && String.Compare(str, "Client_CASModifierPartGenderDmaps") == 0)
                        {
                            Conversion conv = ParseConversion(reader);
                            bool found = false;
                            for (int i = 0; i < this.conversions.Count; i++)
                            {
                                if (this.conversions[i].species == conv.species && this.conversions[i].occult == conv.occult &&
                                    this.conversions[i].age == conv.age && this.conversions[i].gender == conv.gender &&
                                    this.conversions[i].partBodyType == conv.partBodyType && this.conversions[i].partGender == conv.partGender)
                                {
                                    foreach (var c in conv.dmapConversion) this.conversions[i].dmapConversion.Add(c.Key, c.Value);
                                    found = true;
                                    break;
                                }

                            }
                            if (!found) this.conversions.Add(conv);
                        }
                    }
                }
            }
        }

        private Tuning ParseTuning(XmlTextReader reader)
        {
            Dictionary<ulong, float> weights = new Dictionary<ulong, float>();
           // Dictionary<ulong, float> offsets = new Dictionary<ulong, float>();
            Dictionary<ulong, string> names = new Dictionary<ulong, string>();
           // XmlTextReader reader = new XmlTextReader(s);
            string modName = "";
            float val = 0;
            Species species = 0;
            SimOccult occult = 0;
            AgeGender age = 0;
            AgeGender gender = 0;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string str = reader.GetAttribute("n");
                    if (str != null && String.Compare(str, "ModifierName") == 0)
                    {
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Text) modName = reader.Value;
                        ulong hash = FNVhash.FNV64(modName);
                        if (!names.ContainsKey(hash)) names.Add(hash, modName);
                    }
                    else if (str != null && String.Compare(str, "Scale") == 0)
                    {
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Text)
                        {
                            val = float.Parse(reader.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                            ulong hash = FNVhash.FNV64(modName);
                            if (!weights.ContainsKey(hash)) weights.Add(hash, val);
                        }
                    }
                    //else if (str != null && String.Compare(str, "Offset") == 0)
                    //{
                    //    reader.Read();
                    //    if (reader.NodeType == XmlNodeType.Text)
                    //    {
                    //        val = float.Parse(reader.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                    //        ulong hash = FNVhash.FNV64(modName);
                    //        if (!offsets.ContainsKey(hash)) offsets.Add(hash, val);
                    //    }
                    //}
                    else if (String.Compare(str, "Ages") == 0)
                    {
                        using (var innerReader = reader.ReadSubtree())
                        {
                            while (innerReader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    if (String.Compare(reader.Value, "TODDLER") == 0) age |= AgeGender.Toddler;
                                    else if (String.Compare(reader.Value, "CHILD") == 0) age |= AgeGender.Child;
                                    else if (String.Compare(reader.Value, "TEEN") == 0) age |= AgeGender.Teen;
                                    else if (String.Compare(reader.Value, "YOUNGADULT") == 0) age |= AgeGender.YoungAdult;
                                    else if (String.Compare(reader.Value, "ADULT") == 0) age |= AgeGender.Adult;
                                    else if (String.Compare(reader.Value, "ELDER") == 0) age |= AgeGender.Elder;
                                }
                            }                                  
                        }
                    }
                    else if (String.Compare(str, "Genders") == 0)
                    {
                        using (var innerReader = reader.ReadSubtree())
                        {
                            while (innerReader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    if (String.Compare(reader.Value, "MALE") == 0) gender |= AgeGender.Male;
                                    else if (String.Compare(reader.Value, "FEMALE") == 0) gender |= AgeGender.Female;
                                }
                            }
                        }
                    }
                    else if (String.Compare(str, "Species") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "HUMAN") == 0) species |= Species.Human;
                                else if (String.Compare(reader.Value, "SMALLDOG") == 0) species |= Species.LittleDog;
                                else if (String.Compare(reader.Value, "DOG") == 0) species |= Species.Dog;
                                else if (String.Compare(reader.Value, "CAT") == 0) species |= Species.Cat;
                            }
                        }
                    }
                    else if (String.Compare(str, "OccultType") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "HUMAN") == 0) occult |= SimOccult.Human;
                                else if (String.Compare(reader.Value, "VAMPIRE") == 0) occult |= SimOccult.Vampire;
                                else if (String.Compare(reader.Value, "ALIEN") == 0) occult |= SimOccult.Alien;
                                else if (String.Compare(reader.Value, "MERMAID") == 0) occult |= SimOccult.Mermaid;
                                else if (String.Compare(reader.Value, "WITCH") == 0) occult |= SimOccult.Spellcaster;
                            }
                        }
                    }
                }
            }
            if (species == 0) species = Species.Human;
            if (gender == 0) gender = AgeGender.Unisex;
            return new Tuning(species, occult, age, gender, names, weights);
        }
        
        internal class Tuning
        {
            internal Species species;
            internal SimOccult occult;
            internal AgeGender age;
            internal AgeGender gender;
            internal Dictionary<ulong, string> names;
            internal Dictionary<ulong, float> weights;
            // internal Dictionary<ulong, float> offsets;
            internal Tuning(Species species, SimOccult occult, AgeGender age, AgeGender gender, Dictionary<ulong, string> modifierNames, Dictionary<ulong, float> weights)
            {
                this.species = species;
                this.occult = occult;
                this.age = age;
                this.gender = gender;
                this.names = modifierNames;
                this.weights = weights;
               // this.offsets = offsets;
            }
        }

        private Conversion ParseConversion(XmlTextReader reader)
        {
            ulong modHash = 0;
            Species species = 0;
            SimOccult occult = 0;
            AgeGender age = 0;
            AgeGender gender = 0;
            BodyType partBodyType = 0;
            AgeGender partGender = 0;
            bool activeIfNoBreasts = false;
            Dictionary<ulong, string> dmapConversion = new Dictionary<ulong, string>();
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string str = reader.GetAttribute("n");
                    if (str != null && String.Compare(str, "Modifier") == 0)
                    {
                        modHash = 0;
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Text)
                        {
                            string modName = reader.Value;
                            modHash = FNVhash.FNV64(modName);
                        }
                    }
                    else if (str != null && String.Compare(str, "DmapName") == 0)
                    {
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Text)
                        {
                            string dmapName = reader.Value;
                            if (!dmapConversion.ContainsKey(modHash)) dmapConversion.Add(modHash, dmapName);
                        }
                    }
                    else if (String.Compare(str, "Age") == 0)
                    {
                        using (var innerReader = reader.ReadSubtree())
                        {
                            while (innerReader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    if (String.Compare(reader.Value, "TODDLER") == 0) age |= AgeGender.Toddler;
                                    else if (String.Compare(reader.Value, "CHILD") == 0) age |= AgeGender.Child;
                                    else if (String.Compare(reader.Value, "TEEN") == 0) age |= AgeGender.Teen;
                                    else if (String.Compare(reader.Value, "YOUNGADULT") == 0) age |= AgeGender.YoungAdult;
                                    else if (String.Compare(reader.Value, "ADULT") == 0) age |= AgeGender.Adult;
                                    else if (String.Compare(reader.Value, "ELDER") == 0) age |= AgeGender.Elder;
                                }
                            }
                        }
                    }
                    else if (String.Compare(str, "Gender") == 0)
                    {
                        using (var innerReader = reader.ReadSubtree())
                        {
                            while (innerReader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    if (String.Compare(reader.Value, "MALE") == 0) gender |= AgeGender.Male;
                                    else if (String.Compare(reader.Value, "FEMALE") == 0) gender |= AgeGender.Female;
                                }
                            }
                        }
                    }
                    else if (String.Compare(str, "Species") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "HUMAN") == 0) species |= Species.Human;
                                else if (String.Compare(reader.Value, "SMALLDOG") == 0) species |= Species.LittleDog;
                                else if (String.Compare(reader.Value, "DOG") == 0) species |= Species.Dog;
                                else if (String.Compare(reader.Value, "CAT") == 0) species |= Species.Cat;
                            }
                        }
                    }
                    else if (String.Compare(str, "OccultType") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "HUMAN") == 0) occult |= SimOccult.Human;
                                else if (String.Compare(reader.Value, "VAMPIRE") == 0) occult |= SimOccult.Vampire;
                                else if (String.Compare(reader.Value, "ALIEN") == 0) occult |= SimOccult.Alien;
                                else if (String.Compare(reader.Value, "MERMAID") == 0) occult |= SimOccult.Mermaid;
                                else if (String.Compare(reader.Value, "WITCH") == 0) occult |= SimOccult.Spellcaster;
                            }
                        }
                    }
                    else if (String.Compare(str, "ConditionalCasPartBodyType") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                int val = int.Parse(reader.Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                                partBodyType = (BodyType)val;
                            }
                        }
                    }
                    else if (String.Compare(str, "ConditionalCasPartGender") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "MALE") == 0) partGender |= AgeGender.Male;
                                else if (String.Compare(reader.Value, "FEMALE") == 0) partGender |= AgeGender.Female;
                            }
                        }
                    }
                    else if (String.Compare(str, "ActiveIfSimDoesntHaveBreasts") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "True") == 0) activeIfNoBreasts = true;
                                else if (String.Compare(reader.Value, "False") == 0) activeIfNoBreasts = false;
                            }
                        }
                    }
                }
            }
            if (species == 0) species = Species.Human;
            if (gender == 0) gender = AgeGender.Unisex;
            if (occult == 0) occult = SimOccult.Human | SimOccult.Mermaid | SimOccult.Spellcaster;
            return new Conversion(species, occult, age, gender, partBodyType, partGender, activeIfNoBreasts, dmapConversion);
        }

        internal class Conversion
        {
            internal Species species;
            internal SimOccult occult;
            internal AgeGender age;
            internal AgeGender gender;
            internal BodyType partBodyType;
            internal AgeGender partGender;
            internal bool activeIfNoBreasts;
            internal Dictionary<ulong, string> dmapConversion;
            internal Conversion(Species species, SimOccult occult, AgeGender age, AgeGender gender, BodyType partType, AgeGender partGender, 
                bool activeIfNoBreasts, Dictionary<ulong, string> dmapConversion)
            {
                this.species = species;
                this.occult = occult;
                this.age = age;
                this.gender = gender;
                this.partBodyType = partType;
                this.partGender = partGender;
                this.activeIfNoBreasts = activeIfNoBreasts;
                this.dmapConversion = dmapConversion;
            }
        }

        private Dampening ParseDampening(XmlTextReader reader)
        {
            Species species = 0;
            SimOccult occult = 0;
            AgeGender age = 0;
            AgeGender gender = 0;
            List<ulong> smods = new List<ulong>();
            List<Dictionary<ulong, float>> sculptWeights = new List<Dictionary<ulong, float>>();
            ulong modHash = 0, sculptHash = 0;
            Dictionary<ulong, float> temp = new Dictionary<ulong, float>();
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string str = reader.GetAttribute("n");
                    if (str != null && String.Compare(str, "ModifierName") == 0)
                    {
                        if (temp.Count > 0 && modHash > 0)
                        {
                            smods.Add(modHash);
                            sculptWeights.Add(temp);
                            temp = new Dictionary<ulong, float>();
                        }

                        modHash = 0;
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Text)
                        {
                            string modName = reader.Value;
                            modHash = FNVhash.FNV64(modName);
                        }
                    }
                    else if (str != null && String.Compare(str, "SculptName") == 0)
                    {
                        sculptHash = 0;
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Text)
                        {
                            string sculptName = reader.Value;
                            sculptHash = FNVhash.FNV64(sculptName);
                        }
                    }
                    else if (str != null && String.Compare(str, "Limit") == 0)
                    {
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Text)
                        {
                            float val = float.Parse(reader.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                            temp.Add(sculptHash, val);
                        }
                    }
                    else if (String.Compare(str, "Ages") == 0)
                    {
                        using (var innerReader = reader.ReadSubtree())
                        {
                            while (innerReader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    if (String.Compare(reader.Value, "TODDLER") == 0) age |= AgeGender.Toddler;
                                    else if (String.Compare(reader.Value, "CHILD") == 0) age |= AgeGender.Child;
                                    else if (String.Compare(reader.Value, "TEEN") == 0) age |= AgeGender.Teen;
                                    else if (String.Compare(reader.Value, "YOUNGADULT") == 0) age |= AgeGender.YoungAdult;
                                    else if (String.Compare(reader.Value, "ADULT") == 0) age |= AgeGender.Adult;
                                    else if (String.Compare(reader.Value, "ELDER") == 0) age |= AgeGender.Elder;
                                }
                            }
                        }
                    }
                    else if (String.Compare(str, "Genders") == 0)
                    {
                        using (var innerReader = reader.ReadSubtree())
                        {
                            while (innerReader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    if (String.Compare(reader.Value, "MALE") == 0) gender |= AgeGender.Male;
                                    else if (String.Compare(reader.Value, "FEMALE") == 0) gender |= AgeGender.Female;
                                }
                            }
                        }
                    }
                    else if (String.Compare(str, "Species") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "HUMAN") == 0) species |= Species.Human;
                                else if (String.Compare(reader.Value, "SMALLDOG") == 0) species |= Species.LittleDog;
                                else if (String.Compare(reader.Value, "DOG") == 0) species |= Species.Dog;
                                else if (String.Compare(reader.Value, "CAT") == 0) species |= Species.Cat;
                            }
                        }
                    }
                    else if (String.Compare(str, "OccultType") == 0)
                    {
                        reader.Read();
                        {
                            if (reader.NodeType == XmlNodeType.Text)
                            {
                                if (String.Compare(reader.Value, "HUMAN") == 0) occult |= SimOccult.Human;
                                else if (String.Compare(reader.Value, "VAMPIRE") == 0) occult |= SimOccult.Vampire;
                                else if (String.Compare(reader.Value, "ALIEN") == 0) occult |= SimOccult.Alien;
                                else if (String.Compare(reader.Value, "MERMAID") == 0) occult |= SimOccult.Mermaid;
                                else if (String.Compare(reader.Value, "WITCH") == 0) occult |= SimOccult.Spellcaster;
                            }
                        }
                    }
                }
            }
            if (species == 0) species = Species.Human;
            if (occult == 0) occult = SimOccult.Human;
            if (gender == 0) gender = AgeGender.Unisex;
            if (age == 0) age = AgeGender.Child & AgeGender.Adult;
            return new Dampening(species, occult, age, gender, smods, sculptWeights);
        }

        public class Dampening
        {
            internal Species species;
            internal SimOccult occult;
            internal AgeGender age;
            internal AgeGender gender;
            internal List<ulong> modifiers;
            internal List<Dictionary<ulong, float>> sculptLimits;
            internal Dampening(Species species, SimOccult occult, AgeGender age, AgeGender gender, List<ulong> smods, List<Dictionary<ulong, float>> limits)
            {
                this.species = species;
                this.occult = occult;
                this.age = age;
                this.gender = gender;
                this.modifiers = smods;
                this.sculptLimits = limits;
            }
        }
    }
}
