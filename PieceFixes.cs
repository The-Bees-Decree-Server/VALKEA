using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VALKEA
{
    // Fixes to the prefabs in the asset bundle, applied when the mod loads.
    // Each one is also a change to make in the Unity project the next time the bundle is rebuilt.
    internal static class PieceFixes
    {
        private static readonly List<WearNTear> FloatingSpiralStairs = new List<WearNTear>();

        public static void Apply(List<GameObject> pieces)
        {
            foreach (var prefab in pieces)
            {
                FixFireAndAsh(prefab);
                CenterStoneMass(prefab);
            }
            FixGlassCeilingRoof(pieces.First(p => p.name == "piece_TheGlassCeiling"));
            FixLadderCollider(pieces.First(p => p.name == "piece_LadderClimb"));
            FixSmoothStoneBlock3(pieces.First(p => p.name == "piece_SmtStoneBlock3"));
            FloatingSpiralStairs.AddRange(pieces
                .Where(p => new[] { "spiral", "sprial", "sprl" }.Any(p.name.ToLower().Contains))
                .Select(p => p.GetComponent<WearNTear>())
                .Where(w => !w.m_noSupportWear));
        }

        // The bundle carries copies of game materials such as oak_leaf, from the vanilla items and effects it was built
        // with, under the game's names but with Unity 2019 shaders. Mods that look materials up by name, like Seasonality,
        // could take the copy for the game's and gave fall oak leaves its shader, which draws them as opaque blocks.
        public static void RenameBundleMaterials(IEnumerable<Material> materials)
        {
            foreach (var material in materials)
            {
                material.name = "VALKEA_" + material.name;
            }
        }

        // The bundle predates WearNTear.m_burnable (default true) and m_ashDamageResist (default false),
        // so stone, metal and glass caught fire. Set them like the game's stone and iron pieces;
        // iron-material pieces made of wood, rope or paper keep burning, like the game's woodiron_beam.
        private static void FixFireAndAsh(GameObject prefab)
        {
            var wear = prefab.GetComponent<WearNTear>();
            bool stone = wear.m_materialType == WearNTear.MaterialType.Stone;
            bool metal = wear.m_materialType == WearNTear.MaterialType.Iron
                && !new[] { "RopeBridge", "SheaWood", "Crackacorn" }.Any(prefab.name.Contains);
            bool glass = prefab.name.StartsWith("piece_Glass") || prefab.name == "piece_TheGlassCeiling";
            if (stone || metal || glass)
            {
                wear.m_burnable = false;
            }
            if (stone || metal)
            {
                wear.m_ashDamageResist = true;
            }
        }

        // Support is measured from the centre of mass, which is the pivot plus m_comOffset. The stone pieces'
        // pivots sit on an edge (a column's is at its foot), so a column snapped to a floor corner had its
        // centre of mass inside the floor, got no support and broke. The game's own pieces keep it in the middle.
        private static void CenterStoneMass(GameObject prefab)
        {
            var wear = prefab.GetComponent<WearNTear>();
            var boxes = prefab.GetComponentsInChildren<BoxCollider>(true).Where(c => !c.isTrigger).ToList();
            if (wear.m_materialType != WearNTear.MaterialType.Stone || wear.m_comOffset != Vector3.zero || boxes.Count == 0)
            {
                return;
            }
            var bounds = new Bounds(LocalCorners(prefab.transform, boxes[0]).First(), Vector3.zero);
            foreach (var corner in boxes.SelectMany(box => LocalCorners(prefab.transform, box)))
            {
                bounds.Encapsulate(corner);
            }
            wear.m_comOffset = bounds.center;
        }

        private static IEnumerable<Vector3> LocalCorners(Transform root, BoxCollider box)
        {
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                yield return root.InverseTransformPoint(box.transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f, sign)));
            }
        }

        // The Glass Ceiling's collider was tagged "leaky" like the game's wood floors, so the game didn't count it
        // as a roof: rain fell through it, fires under it went out and snow settled under it. Tag it like stone floors.
        private static void FixGlassCeilingRoof(GameObject ceiling)
        {
            foreach (var collider in ceiling.GetComponentsInChildren<Collider>(true))
            {
                collider.gameObject.tag = "roof";
            }
        }

        // The Miofellian Ladder's only collider was non-solid, so players walked through it; E still climbs it
        private static void FixLadderCollider(GameObject ladder)
        {
            int piece = LayerMask.NameToLayer("piece");
            foreach (var collider in ladder.GetComponentsInChildren<Collider>(true))
            {
                collider.gameObject.layer = piece;
            }
        }

        // Smooth Stone Block III snapped and collided as 3.03 x 1.89 x 1.04 instead of 3 x 2 x 1,
        // so it didn't line up with the other blocks. Centre and size match Block V's grid.
        private static void FixSmoothStoneBlock3(GameObject block)
        {
            var center = new Vector3(0.27f, 0.46f, 0f);
            var half = new Vector3(1.5f, 1f, 0.5f);
            foreach (Transform child in block.transform)
            {
                if (child.CompareTag("snappoint"))
                {
                    var side = child.localPosition - center;
                    child.localPosition = center + Vector3.Scale(half, new Vector3(Mathf.Sign(side.x), Mathf.Sign(side.y), Mathf.Sign(side.z)));
                }
                else if (child.GetComponent<BoxCollider>())
                {
                    child.localPosition = center;
                    child.localScale = 2f * half;
                    child.gameObject.layer = LayerMask.NameToLayer("piece");
                }
            }
        }

        // The v2 spiral staircases and the spiral columns skip the support check, so they never fall.
        // Turning it on applies to pieces loaded after the change.
        public static void SetSpiralStairsNeedSupport(bool needSupport)
        {
            foreach (var wear in FloatingSpiralStairs)
            {
                wear.m_noSupportWear = needSupport;
            }
        }
    }
}
