using UnityEngine;

namespace AddressablesInspector
{
    public class PaginationSettings
    {
        public int SortingOption { get; set; } // 0 - undefined, 1 - size desc, 2 - size asc, 3 - refs desc, 4 - refs asc
        
        public int? PageToShow { get; set; } = 0;
        public int PageSize { get; set; } = 10;
        public Vector2 Scroll { get; set; }
    }
}