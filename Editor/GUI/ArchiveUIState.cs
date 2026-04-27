using System.Collections.Generic;
using UnityEngine;

namespace AddressablesInspector
{
    public class ArchiveUIState
    {
        public bool ReferencedByBundlesDirectlyFoldout { get; set; }
        public bool ReferencedByBundlesExpandedFoldout { get; set; }
        public string SearchFilter { get; set; }

        public ArchiveUIState()
        {
            ReferencedByBundlesDirectlyFoldout = false;
            ReferencedByBundlesExpandedFoldout = false;
            SearchFilter = string.Empty;
        }
    }

    public class AssetUIState
    {
        public bool ExternalRefsFoldout { get; set; }
        public bool ReferencedByBundlesFoldout { get; set; }
        public string SearchFilter { get; set; }
        public bool ShowExternalReferencesToRemoteOnly { get; set; }
        public Vector2 ExternalRefsScroll { get; set; }

        public AssetUIState()
        {
            ExternalRefsFoldout = false;
            ReferencedByBundlesFoldout = false;
            SearchFilter = string.Empty;
            ShowExternalReferencesToRemoteOnly = false;
        }
    }
}