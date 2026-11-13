using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>The town of Сухой Брод from the air and from its streets: the main street both ways, the side street
        /// up to the church, the adobe quarter, the yards behind, the stock pens by the depot, by day and by night.</summary>
        IEnumerator TownLook()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            g.scriptLock = true;
            P.looking = false;
            foreach (string key in new[] { "barber_door", "paper_door", "undertaker_door", "rooms_door", "laundry_door", "feed_door", "cantina_door", "adobe_n1_door", "adobe_s3_door", "stock_pens" })
                Check(r.points.ContainsKey(key), "the town has " + key);
            Vector3 west = r.P("town_west"), east = r.P("town_east"), church = r.P("church_door");
            Vector3 along = (east - west).normalized, north = Vector3.Cross(along, Vector3.up);
            Vector3 mid = (west + east) * 0.5f;
            P.Teleport(west - along * 30f, 0f);
            yield return Wait(6f);

            yield return BeautyShot("aerial", mid - along * 120f - north * 110f + Vector3.up * 70f, mid + along * 10f, 50f, 13f);
            yield return BeautyShot("street_from_west", west + along * 4f - north * 2.5f + Vector3.up * 1.7f, east + Vector3.up * 2f, 55f, 10f);
            yield return BeautyShot("street_from_east", east - along * 6f + north * 3f + Vector3.up * 1.7f, west + Vector3.up * 4f, 55f, 17.6f);
            Vector3 cross = new Vector3(church.x, mid.y, mid.z);
            yield return BeautyShot("side_street_church", cross - north * 9f + Vector3.up * 1.7f, church + Vector3.up * 4f, 55f, 16f);
            Vector3 adobe = r.P("adobe_s3_door");
            yield return BeautyShot("adobe_quarter", adobe + along * 9f + north * 6f + Vector3.up * 1.7f, adobe - along * 6f - north * 2f + Vector3.up * 1.5f, 55f, 18.2f);
            yield return BeautyShot("saloon_hall", r.P("saloon_door") + north * 4.6f + Vector3.up * 1.6f, r.P("saloon_door") + north * 12f + along * 3f + Vector3.up * 1.4f, 60f, 20.5f);
            Vector3 yard = r.P("store_door") + north * 22f;
            yield return BeautyShot("yards_north", yard + along * 16f + north * 6f + Vector3.up * 2.2f, yard - along * 14f - north * 4f + Vector3.up * 1f, 55f, 11f);
            Vector3 laundry = r.P("laundry_door") - north * 14f;
            yield return BeautyShot("yards_south", laundry - along * 10f - north * 6f + Vector3.up * 2f, laundry + along * 10f + north * 2f, 55f, 9f);
            Vector3 pens = r.P("stock_pens");
            yield return BeautyShot("stock_pens", pens + along * 24f + north * 14f + Vector3.up * 5f, pens, 55f, 15f);
            Vector3 und = r.P("undertaker_door");
            yield return BeautyShot("porches", und - along * 7f - north * 2.5f + Vector3.up * 1.6f, und + along * 3f + north * 3f + Vector3.up * 1.4f, 55f, 14f);
            // the painted ads on their blank walls, and the well at the crossing
            foreach (var (id, side, w, d) in new[] { ("office", 1f, 9f, 9f), ("bank", -1f, 10f, 10f) })
            {
                GameObject b = GameObject.Find("B_" + id);
                Check(b != null, "the " + id + " stands");
                if (b == null) continue;
                Vector3 wall = b.transform.TransformPoint(new Vector3(side * w * 0.5f, 2.7f, d * 0.5f));
                Vector3 eye = b.transform.TransformPoint(new Vector3(side * (w * 0.5f + 10f), 1.8f, d * 0.5f - 2.5f));
                yield return BeautyShot(id + "_ad", eye, wall, 55f, 15f);
            }
            Check(r.points.ContainsKey("town_well"), "the town has its well");
            Vector3 wellAt = r.P("town_well");
            yield return BeautyShot("well", wellAt + along * 4.5f - north * 3f + Vector3.up * 2.4f, wellAt + Vector3.up * 1.5f, 55f, 15f);
            yield return BeautyShot("night_street", west + along * 30f + north * 1f + Vector3.up * 1.7f, east + Vector3.up * 2f, 55f, 22.5f);
            yield return BeautyShot("from_the_rise", mid - along * 70f + north * 95f + Vector3.up * 24f, mid + along * 15f, 50f, 18.4f);
        }
    }
}
