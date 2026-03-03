namespace OuterWildsAccess
{
    /// <summary>
    /// Navigable help menu opened with F1.
    ///
    /// Two-level submenu navigation:
    ///   Level 1 (Categories): Arrow Up/Down to browse, Enter to open
    ///   Level 2 (Items):      Arrow Up/Down to browse, Backspace to go back
    ///   Escape — close from any level
    ///
    /// All other mod hotkeys are blocked while the menu is open.
    /// </summary>
    public class HelpMenu
    {
        #region Inner types

        private enum Level { Closed, Categories, Items }

        private struct HelpItem
        {
            public string KeyLocKey;
            public string DescLocKey;
        }

        #endregion

        #region Constants

        private const int CategoryCount = 6;

        #endregion

        #region State

        private Level _level = Level.Closed;
        private int _catIndex;
        private int _itemIndex;

        private string[] _categoryNames;
        private HelpItem[][] _categories;

        #endregion

        #region Public API

        /// <summary>True while the help menu is open — blocks other hotkeys.</summary>
        public bool IsOpen => _level != Level.Closed;

        /// <summary>Initializes the static help data. Call from Main.Start().</summary>
        public void Initialize()
        {
            BuildData();
        }

        /// <summary>Opens the help menu at the categories level.</summary>
        public void Open()
        {
            if (_level != Level.Closed) return;
            _level = Level.Categories;
            _catIndex = 0;
            _itemIndex = 0;

            ScreenReader.Say(Loc.Get("help_open"));
            AnnounceCurrentCategory();
            DebugLogger.LogInput("F1", "HelpMenu opened");
        }

        /// <summary>Closes the help menu from any level.</summary>
        public void Close()
        {
            if (_level == Level.Closed) return;
            _level = Level.Closed;
            ScreenReader.Say(Loc.Get("help_close"));
            DebugLogger.LogInput("Escape", "HelpMenu closed");
        }

        /// <summary>Enter: drills down from categories to items.</summary>
        public void DrillDown()
        {
            if (_level != Level.Categories) return;
            _level = Level.Items;
            _itemIndex = 0;

            string catName = Loc.Get(_categoryNames[_catIndex]);
            int count = _categories[_catIndex].Length;
            ScreenReader.Say(Loc.Get("help_cat_entered", catName, count));
            AnnounceCurrentItem();
        }

        /// <summary>Backspace: goes back from items to categories.</summary>
        public void GoBack()
        {
            if (_level == Level.Items)
            {
                _level = Level.Categories;
                AnnounceCurrentCategory();
            }
            else if (_level == Level.Categories)
            {
                Close();
            }
        }

        /// <summary>Arrow Down: next category or next item depending on level.</summary>
        public void CycleNext()
        {
            if (_level == Level.Categories)
            {
                _catIndex = (_catIndex + 1) % CategoryCount;
                AnnounceCurrentCategory();
            }
            else if (_level == Level.Items)
            {
                HelpItem[] items = _categories[_catIndex];
                if (items.Length == 0) return;
                _itemIndex = (_itemIndex + 1) % items.Length;
                AnnounceCurrentItem();
            }
        }

        /// <summary>Arrow Up: previous category or previous item depending on level.</summary>
        public void CyclePrev()
        {
            if (_level == Level.Categories)
            {
                _catIndex = (_catIndex - 1 + CategoryCount) % CategoryCount;
                AnnounceCurrentCategory();
            }
            else if (_level == Level.Items)
            {
                HelpItem[] items = _categories[_catIndex];
                if (items.Length == 0) return;
                _itemIndex = (_itemIndex - 1 + items.Length) % items.Length;
                AnnounceCurrentItem();
            }
        }

        #endregion

        #region Announcements

        private void AnnounceCurrentCategory()
        {
            string catName = Loc.Get(_categoryNames[_catIndex]);
            int count = _categories[_catIndex].Length;
            ScreenReader.Say(Loc.Get("help_category", catName, count));
        }

        private void AnnounceCurrentItem()
        {
            HelpItem[] items = _categories[_catIndex];
            if (items.Length == 0) return;

            HelpItem item = items[_itemIndex];
            string key = Loc.Get(item.KeyLocKey);
            string desc = Loc.Get(item.DescLocKey);
            ScreenReader.Say(Loc.Get("help_item", key, desc));
        }

        #endregion

        #region Data

        private void BuildData()
        {
            _categoryNames = new string[]
            {
                "help_cat_general",
                "help_cat_navigation",
                "help_cat_status",
                "help_cat_ship",
                "help_cat_tools",
                "help_cat_settings",
            };

            _categories = new HelpItem[CategoryCount][];

            // Category 0: Général
            _categories[0] = new HelpItem[]
            {
                new HelpItem { KeyLocKey = "help_key_f1",        DescLocKey = "help_desc_f1" },
                new HelpItem { KeyLocKey = "help_key_f2",        DescLocKey = "help_desc_f2" },
                new HelpItem { KeyLocKey = "help_key_f3",        DescLocKey = "help_desc_f3" },
                new HelpItem { KeyLocKey = "help_key_f12",       DescLocKey = "help_desc_f12" },
                new HelpItem { KeyLocKey = "help_key_delete",    DescLocKey = "help_desc_delete" },
                new HelpItem { KeyLocKey = "help_key_backspace", DescLocKey = "help_desc_backspace" },
            };

            // Category 1: Navigation
            _categories[1] = new HelpItem[]
            {
                new HelpItem { KeyLocKey = "help_key_home",       DescLocKey = "help_desc_home_nav" },
                new HelpItem { KeyLocKey = "help_key_pageupdown", DescLocKey = "help_desc_pageupdown_nav" },
                new HelpItem { KeyLocKey = "help_key_altpage",    DescLocKey = "help_desc_altpage" },
                new HelpItem { KeyLocKey = "help_key_end",        DescLocKey = "help_desc_end_nav" },
                new HelpItem { KeyLocKey = "help_key_l",          DescLocKey = "help_desc_l" },
                new HelpItem { KeyLocKey = "help_key_g",          DescLocKey = "help_desc_g" },
                new HelpItem { KeyLocKey = "help_key_m",          DescLocKey = "help_desc_m" },
                new HelpItem { KeyLocKey = "help_key_t",          DescLocKey = "help_desc_t" },
            };

            // Category 2: État
            _categories[2] = new HelpItem[]
            {
                new HelpItem { KeyLocKey = "help_key_h", DescLocKey = "help_desc_h" },
                new HelpItem { KeyLocKey = "help_key_j", DescLocKey = "help_desc_j" },
                new HelpItem { KeyLocKey = "help_key_k", DescLocKey = "help_desc_k" },
            };

            // Category 3: Vaisseau
            _categories[3] = new HelpItem[]
            {
                new HelpItem { KeyLocKey = "help_key_i",          DescLocKey = "help_desc_i" },
                new HelpItem { KeyLocKey = "help_key_home",       DescLocKey = "help_desc_home_pilot" },
                new HelpItem { KeyLocKey = "help_key_pageupdown", DescLocKey = "help_desc_pageupdown_pilot" },
                new HelpItem { KeyLocKey = "help_key_end",        DescLocKey = "help_desc_end_pilot" },
                new HelpItem { KeyLocKey = "help_key_f3",         DescLocKey = "help_desc_f3" },
            };

            // Category 4: Outils
            _categories[4] = new HelpItem[]
            {
                new HelpItem { KeyLocKey = "help_key_u",  DescLocKey = "help_desc_u" },
                new HelpItem { KeyLocKey = "help_key_o",  DescLocKey = "help_desc_o" },
                new HelpItem { KeyLocKey = "help_key_f4", DescLocKey = "help_desc_f4" },
            };

            // Category 5: Réglages
            _categories[5] = new HelpItem[]
            {
                new HelpItem { KeyLocKey = "help_key_f5", DescLocKey = "help_desc_f5" },
                new HelpItem { KeyLocKey = "help_key_f6", DescLocKey = "help_desc_f6" },
            };
        }

        #endregion
    }
}
