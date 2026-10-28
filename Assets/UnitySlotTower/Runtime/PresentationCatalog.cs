using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Runtime
{
    /// <summary>CSV（TextAsset）と、数値でない参照（プレハブ・アイコン・音）を CSV の id で結びつける。</summary>
    [CreateAssetMenu(menuName = "UnitySlotTower/Presentation Catalog")]
    public sealed class PresentationCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public GameObject prefab;
            public Sprite icon;
            public AudioClip sound;
        }

        [SerializeField] private TextAsset[] tables = Array.Empty<TextAsset>();
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, Entry> entryById;

        /// <summary>TextAsset の名前（拡張子なし）が CSV ファイル名と一致するものを返す。</summary>
        public string ReadTable(string csvFileName)
        {
            foreach (var table in tables)
                if (table != null && table.name + ".csv" == csvFileName) return table.text;
            return null;
        }

        public TableSet LoadTables() => TableSet.Load(ReadTable);

        public bool TryGetEntry(string id, out Entry entry)
        {
            if (entryById == null)
            {
                entryById = new Dictionary<string, Entry>(entries.Length);
                foreach (var e in entries) entryById[e.id] = e;
            }
            return entryById.TryGetValue(id, out entry);
        }

        private void OnValidate() => entryById = null;
    }
}
