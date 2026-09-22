using System.Collections.Generic;

namespace YMTEditor
{
    public class YMTTypes
    {
        /// <summary>
        /// What each slot actually holds, shown next to the jbib/accs/... names so they
        /// don't have to be memorised. Wording follows the Help menu.
        /// </summary>
        private static readonly Dictionary<string, string> SlotLabels = new Dictionary<string, string>
        {
            { "head", "head models" },
            { "berd", "masks, beards" },
            { "hair", "hair" },
            { "uppr", "torso, arms" },
            { "lowr", "pants, legs" },
            { "hand", "bags, backpacks" },
            { "feet", "shoes" },
            { "teef", "ties, scarves, necklaces" },
            { "accs", "undershirts" },
            { "task", "vests" },
            { "decl", "decals, stickers" },
            { "jbib", "shirts, hoodies, jackets" },
            { "p_head", "hats, helmets" },
            { "p_eyes", "glasses" },
            { "p_ears", "earrings" },
            { "p_mouth", "mouth" },
            { "p_lhand", "left hand" },
            { "p_rhand", "right hand" },
            { "p_lwrist", "watches" },
            { "p_rwrist", "bracelets" },
            { "p_hip", "hip" },
            { "p_lfoot", "left foot" },
            { "p_rfoot", "right foot" },
            { "p_ph_l_hand", "left hand (phone)" },
            { "p_ph_r_hand", "right hand (phone)" },
        };

        public static string SlotLabel(string slot)
        {
            string label;
            return SlotLabels.TryGetValue((slot ?? "").ToLower(), out label) ? label : "";
        }

        public enum ComponentNumbers
        {
            head = 0,
            berd = 1,
            hair = 2,
            uppr = 3,
            lowr = 4,
            hand = 5,
            feet = 6,
            teef = 7,
            accs = 8,
            task = 9,
            decl = 10,
            jbib = 11
        }

        public enum PropNumbers
        {
            p_head = 0,
            p_eyes = 1,
            p_ears = 2,
            p_mouth = 3, //unused?
            p_lhand = 4, //unused?
            p_rhand = 5, //unused?
            p_lwrist = 6,
            p_rwrist = 7,
            p_hip = 8, //unused?
            p_lfoot = 9, //unused?
            p_rfoot = 10, //unused?
            p_ph_l_hand = 11, //unused? - not sure about this name
            p_ph_r_hand = 12 //unused? - not sure about this name
        }
    }
}
