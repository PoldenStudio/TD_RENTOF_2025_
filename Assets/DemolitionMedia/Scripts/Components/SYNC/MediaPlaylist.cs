using System.Collections.Generic;
using UnityEngine;

namespace DemolitionStudios.DemolitionMedia
{
    [CreateAssetMenu(fileName = "MediaPlaylist", menuName = "Demolition Media/Media Playlist", order = 1)]
    public class MediaPlaylist : ScriptableObject
    {
        [SerializeField] public List<string> Files;
        [SerializeField] public List<string> AudioFiles;

        public bool empty()
        {
            return Files.Count == 0;
        }
    }
}