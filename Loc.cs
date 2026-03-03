using System;
using System.Collections.Generic;

namespace OuterWildsAccess
{
    /// <summary>
    /// Localization for OuterWildsAccess.
    /// Supports French and English. Detects game language automatically.
    /// Falls back to English for unsupported languages.
    ///
    /// Usage:
    ///   Loc.Get("key")             — get a string
    ///   Loc.Get("key", arg1, arg2) — get a string with {0}, {1} placeholders
    /// </summary>
    public static class Loc
    {
        #region Fields

        private static bool _initialized = false;
        private static readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _keyLabels = new Dictionary<string, string>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes localization. Call once at mod startup.
        /// Detects game language and loads the appropriate string table.
        /// </summary>
        public static void Initialize()
        {
            bool isFrench = false;
            try
            {
                var lang = TextTranslation.Get().GetLanguage();
                isFrench = (lang == TextTranslation.Language.FRENCH);
            }
            catch
            {
                // TextTranslation not ready — default to English
            }

            if (isFrench)
            {
                InitializeFrench();
                InitializeKeyLabelsFrench();
            }
            else
            {
                InitializeEnglish();
                InitializeKeyLabelsEnglish();
            }

            _initialized = true;
        }

        /// <summary>
        /// Returns the localized string for the given key.
        /// Falls back to the key itself if not found.
        /// </summary>
        public static string Get(string key)
        {
            if (!_initialized) Initialize();

            if (_strings.TryGetValue(key, out string value))
                return value;

            return key;
        }

        /// <summary>
        /// Translates a raw key/button label returned by InputTransitionUtil.
        /// Handles all-caps English fallback names (e.g. "CONFIRM" → "Confirmer").
        /// Returns the label unchanged if no mapping is found.
        /// </summary>
        public static string LocalizeKeyLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return label;
            if (_keyLabels.TryGetValue(label, out string localized)) return localized;
            return label;
        }

        /// <summary>
        /// Returns the localized string with {0}, {1}... placeholders replaced.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        #endregion

        #region French Strings

        private static void InitializeKeyLabelsFrench()
        {
            _keyLabels["CONFIRM"]    = "Confirmer";
            _keyLabels["CANCEL"]     = "Annuler";
            _keyLabels["INTERACT"]   = "Interagir";
            _keyLabels["JUMP"]       = "Sauter";
            _keyLabels["ENTER"]      = "Entrée";
            _keyLabels["BACK"]       = "Retour";
            _keyLabels["PAUSE"]      = "Pause";
            _keyLabels["MAP"]        = "Carte";
            _keyLabels["SUIT"]       = "Combinaison";
            _keyLabels["FLASHLIGHT"] = "Lampe torche";
        }

        private static void InitializeFrench()
        {
            // ===== GENERAL =====
            _strings["mod_loaded"]  = "Outer Wilds Access chargé. F1 pour l'aide.";
            _strings["debug_on"]    = "Mode débogage activé.";
            _strings["debug_off"]   = "Mode débogage désactivé.";
            _strings["mod_disabled"] = "Outer Wilds Access désactivé.";
            _strings["mod_enabled"]  = "Outer Wilds Access activé.";

            // ===== HELP MENU (F1) =====
            _strings["help_open"]        = "Aide ouverte. Flèches pour naviguer. Entrée pour ouvrir une catégorie. Retour arrière pour revenir. Escape pour fermer.";
            _strings["help_close"]       = "Aide fermée.";
            _strings["help_category"]    = "{0}, {1} raccourcis.";
            _strings["help_cat_entered"] = "{0}, {1} raccourcis.";
            _strings["help_item"]        = "{0} : {1}";

            // Category names
            _strings["help_cat_general"]    = "Général";
            _strings["help_cat_navigation"] = "Navigation";
            _strings["help_cat_status"]     = "État";
            _strings["help_cat_ship"]       = "Vaisseau";
            _strings["help_cat_tools"]      = "Outils";
            _strings["help_cat_settings"]   = "Réglages";

            // Key names
            _strings["help_key_f1"]         = "F1";
            _strings["help_key_f2"]         = "F2";
            _strings["help_key_f3"]         = "F3";
            _strings["help_key_f4"]         = "F4";
            _strings["help_key_f5"]         = "F5";
            _strings["help_key_f6"]         = "F6";
            _strings["help_key_f12"]        = "F12";
            _strings["help_key_delete"]     = "Suppr";
            _strings["help_key_backspace"]  = "Retour arrière";
            _strings["help_key_home"]       = "Début";
            _strings["help_key_end"]        = "Fin";
            _strings["help_key_pageupdown"] = "Page suivante et précédente";
            _strings["help_key_altpage"]    = "Alt + Page suivante et précédente";
            _strings["help_key_g"]          = "G";
            _strings["help_key_h"]          = "H";
            _strings["help_key_i"]          = "I";
            _strings["help_key_j"]          = "J";
            _strings["help_key_k"]          = "K";
            _strings["help_key_l"]          = "L";
            _strings["help_key_o"]          = "O";
            _strings["help_key_t"]          = "T";
            _strings["help_key_u"]          = "U";

            // Descriptions
            _strings["help_desc_f1"]        = "Aide — ouvre ce menu";
            _strings["help_desc_f2"]        = "Temps restant avant la supernova";
            _strings["help_desc_f3"]        = "Rappeler le vaisseau au-dessus de vous";
            _strings["help_desc_f4"]        = "Journal de bord — parcourir les découvertes";
            _strings["help_desc_f5"]        = "Désactiver ou réactiver le mod";
            _strings["help_desc_f6"]        = "Ouvrir le menu réglages";
            _strings["help_desc_f12"]       = "Activer ou désactiver le mode debug";
            _strings["help_desc_delete"]    = "Répéter la dernière annonce";
            _strings["help_desc_backspace"] = "Couper ou rétablir la balise audio";
            _strings["help_desc_home_nav"]  = "Scanner les objets proches";
            _strings["help_desc_pageupdown_nav"]  = "Parcourir les objets scannés";
            _strings["help_desc_altpage"]   = "Changer de catégorie de navigation";
            _strings["help_desc_end_nav"]   = "Distance et direction vers la cible";
            _strings["help_desc_l"]         = "Position détaillée — planète, zone et lieu proche";
            _strings["help_desc_g"]         = "Guidage audio vers la cible par tics sonores";
            _strings["help_desc_o"]         = "Marche automatique vers la cible";
            _strings["help_desc_t"]         = "Téléportation vers la cible — même planète, 500 mètres maximum";
            _strings["help_desc_h"]         = "État personnel — santé, oxygène, jetpack, boost, combinaison";
            _strings["help_desc_j"]         = "État du vaisseau — carburant, oxygène, coque, dégâts";
            _strings["help_desc_k"]         = "Environnement — dangers actifs, gravité, eau";
            _strings["help_desc_i"]         = "Télémétrie de vol — vitesse, altitude, dégâts";
            _strings["help_desc_home_pilot"]       = "Sélectionner une destination pour l'autopilote — aux commandes";
            _strings["help_desc_pageupdown_pilot"] = "Parcourir les planètes — aux commandes";
            _strings["help_desc_end_pilot"]        = "Lancer l'autopilote vers la destination — aux commandes";
            _strings["help_desc_u"]         = "Statut du scope de signal — fréquence et signal détecté";

            // ===== MENU HANDLER =====
            _strings["toggle_on"]       = "Activé";
            _strings["toggle_off"]      = "Désactivé";
            _strings["slider_value"]    = "{0} sur 10";
            _strings["rebinding_enter"] = "Appuyez sur une touche à assigner.";
            _strings["rebinding_done"]  = "Touche assignée : {0}.";
            _strings["rebinding_cancel"]= "Annulé.";

            // ===== STATE HANDLER =====
            // Death causes
            _strings["death_default"]          = "Mort.";
            _strings["death_impact"]           = "Mort à l'impact.";
            _strings["death_asphyxiation"]     = "Asphyxie — manque d'oxygène.";
            _strings["death_energy"]           = "Électrocution.";
            _strings["death_supernova"]        = "Consumé par la supernova.";
            _strings["death_digestion"]        = "Digéré par la plante.";
            _strings["death_bigbang"]          = "Fin de la boucle — le soleil explose.";
            _strings["death_crushed"]          = "Écrasé.";
            _strings["death_meditation"]       = "Méditation — passage au prochain cycle.";
            _strings["death_timeloop"]         = "Fin du cycle temporel.";
            _strings["death_lava"]             = "Mort dans la lave.";
            _strings["death_blackhole"]        = "Aspiré dans un trou noir.";
            _strings["death_dream"]            = "Mort dans le rêve.";
            _strings["death_dreamexplosion"]   = "Explosion dans le rêve.";
            _strings["death_crushedbyelevator"] = "Écrasé par l'ascenseur.";

            // Respawn / cycle
            _strings["player_respawn"]         = "Nouveau cycle. Tu es de retour au vaisseau.";

            // Ship
            _strings["enter_ship"]             = "Dans le vaisseau.";
            _strings["exit_ship"]              = "Sorti du vaisseau.";
            _strings["enter_flight_console"]   = "Aux commandes du vaisseau.";
            _strings["exit_flight_console"]    = "Commandes quittées.";
            _strings["ship_hull_breach"]       = "Attention — coque du vaisseau endommagée !";
            _strings["enter_ship_computer"]    = "Journal de bord ouvert.";
            _strings["exit_ship_computer"]     = "Journal de bord fermé.";
            _strings["enter_landing_view"]     = "Caméra d'atterrissage activée.";
            _strings["exit_landing_view"]      = "Caméra d'atterrissage désactivée.";

            // Equipment
            _strings["suit_on"]                = "Combinaison enfilée.";
            _strings["suit_off"]               = "Combinaison retirée.";
            _strings["flashlight_on"]          = "Lampe torche allumée.";
            _strings["flashlight_off"]         = "Lampe torche éteinte.";
            _strings["equip_signalscope"]      = "Scope de signal équipé.";
            _strings["unequip_signalscope"]    = "Scope de signal rangé.";
            _strings["equip_translator"]       = "Traducteur équipé.";
            _strings["unequip_translator"]     = "Traducteur rangé.";

            // Map
            _strings["enter_map"]              = "Carte du système solaire ouverte.";
            _strings["exit_map"]               = "Carte fermée.";

            // Time
            _strings["fast_forward_start"]     = "Avance rapide du temps.";
            _strings["fast_forward_end"]       = "Temps normal.";

            // Conversation
            _strings["enter_conversation"]     = "Dialogue.";
            _strings["exit_conversation"]      = "Dialogue terminé.";

            // Signalscope
            _strings["enter_signalscope"]      = "Scope de signal activé.";
            _strings["exit_signalscope"]       = "Scope de signal désactivé.";

            // ===== NAVIGATION HANDLER =====
            _strings["nav_ship"]          = "Vaisseau";
            _strings["nav_model_rocket"]  = "Fusée modèle réduit";
            _strings["nav_nothing_found"] = "Aucun objet à proximité.";
            _strings["nav_scan_first"]    = "{0} objet(s) trouvé(s). {1}, {2} mètres.";
            _strings["nav_item"]          = "{0} sur {1} : {2}, {3} mètres.";
            _strings["nav_stale"]         = "Liste ancienne — appuyez sur Début pour actualiser.";
            _strings["nav_no_scan"]       = "Scannez d'abord avec la touche Début.";
            _strings["nav_no_target"]     = "Sélectionnez un objet avec Page suivante, puis appuyez sur Fin.";
            _strings["nav_target_lost"]    = "Cible perdue.";
            _strings["nav_target_cleared"] = "Ancrage retiré.";
            _strings["nav_navigate"]      = "{0} : {1}, {2}m";
            _strings["nav_interact_hint"] = "Appuyez sur {0} pour interagir.";
            _strings["nav_north"]         = "devant";
            _strings["nav_south"]         = "derrière";
            _strings["nav_east"]          = "à droite";
            _strings["nav_west"]          = "à gauche";
            _strings["nav_up"]            = "en haut";
            _strings["nav_down"]          = "en bas";
            _strings["nav_here"]          = "ici";
            _strings["nav_live"]          = "{0}, {1}m";

            // ===== CATEGORY NAVIGATION (Alt+PageUp/Down) =====
            _strings["nav_cat_ship"]           = "Vaisseau";
            _strings["nav_cat_npcs"]           = "Personnages";
            _strings["nav_cat_interactables"]  = "Objets interactifs";
            _strings["nav_cat_nomai"]          = "Textes nomaï";
            _strings["nav_cat_locations"]      = "Lieux";
            _strings["nav_cat_signs"]          = "Panneaux";
            _strings["nav_cat_announce"]       = "{0} : {1} résultat(s). {2}, {3} mètres.";
            _strings["nav_cat_empty"]          = "{0} : aucun résultat.";

            // Nomai text labels (object scan)
            _strings["nav_nomai_wall"]       = "Texte nomaï mural";
            _strings["nav_nomai_computer"]   = "Ordinateur nomaï";

            // Campfire labels
            _strings["nav_campfire"]            = "Feu de camp";
            _strings["nav_campfire_lit"]         = "allumé";
            _strings["nav_campfire_smoldering"]  = "braises";
            _strings["nav_campfire_unlit"]       = "éteint";

            // Sub-sector FR translations
            _strings["sector_village"]             = "Village";
            _strings["sector_zerogcave"]           = "Grotte en apesanteur";
            _strings["sector_observatory"]         = "Observatoire";
            _strings["sector_museum"]              = "Musée";
            _strings["sector_north_pole"]          = "Pôle nord";
            _strings["sector_south_pole"]          = "Pôle sud";
            _strings["sector_crossroads"]          = "Carrefour";
            _strings["sector_canyons"]             = "Canyons";
            _strings["sector_anglerfish"]          = "Poisson-lanterne";
            _strings["sector_oldsettle"]           = "Ancien camp";
            _strings["sector_gravitycannon"]       = "Canon gravitationnel";
            _strings["sector_towerofknowledge"]    = "Tour du Savoir";
            _strings["sector_blackholeforge"]      = "Forge du Trou Noir";
            _strings["sector_hangingcity"]         = "Cité Suspendue";
            _strings["sector_constructionyard"]    = "Chantier de construction";
            _strings["sector_escape_pod"]          = "Module d'évacuation";
            _strings["sector_thlanding"]           = "Zone d'atterrissage";
            _strings["sector_geyser"]              = "Geyser";
            _strings["sector_undergroundlake"]     = "Lac souterrain";
            _strings["sector_quantumgrove"]        = "Bosquet quantique";
            _strings["sector_quantumcaves"]        = "Grottes quantiques";

            // ===== LOCATION HANDLER =====
            _strings["location_enter"]         = "Arrivée : {0}.";
            _strings["location_current"]       = "Position : {0}.";
            _strings["location_space"]         = "En orbite dans l'espace.";
            _strings["location_unknown"]       = "Position inconnue.";
            _strings["location_near"]          = "près de {0}";

            // Planet / zone names
            _strings["loc_sun"]                = "le Soleil";
            _strings["loc_ash_twin"]           = "Jumeau des cendres";
            _strings["loc_ember_twin"]         = "Jumeau des braises";
            _strings["loc_hourglass_twins"]    = "Jumeaux des sabliers";
            _strings["loc_timber_hearth"]      = "Foyer des Bois";
            _strings["loc_brittle_hollow"]     = "Creux Friable";
            _strings["loc_giants_deep"]        = "Profondeur du Géant";
            _strings["loc_dark_bramble"]       = "Ronce Obscure";
            _strings["loc_comet"]              = "L'Intrus";
            _strings["loc_quantum_moon"]       = "Lune Quantique";
            _strings["loc_timber_moon"]        = "Attleroche";
            _strings["loc_volcanic_moon"]      = "Lanterne du Creux";
            _strings["loc_bramble_dimension"]  = "Dimension de la Ronce";
            _strings["loc_probe_cannon"]       = "Canon à sondes orbital";
            _strings["loc_eye"]                = "l'Œil de l'Univers";
            _strings["loc_sun_station"]        = "Station Solaire";
            _strings["loc_white_hole"]         = "Trou Blanc";
            _strings["loc_time_loop_device"]   = "Dispositif de boucle temporelle";
            _strings["loc_vessel"]             = "le Vaisseau nomaï";
            _strings["loc_vessel_dimension"]   = "Dimension du Vaisseau nomaï";
            _strings["loc_dream_world"]        = "Monde des Rêves";
            _strings["loc_invisible_planet"]   = "Planète invisible";

            // Environment
            _strings["camera_enter_water"]     = "Caméra sous l'eau.";
            _strings["attach_to_point"]        = "Fixé à une surface.";
            _strings["detach_from_point"]      = "Détaché de la surface.";
            _strings["enter_undertow"]         = "Courant de succion.";
            _strings["exit_undertow"]          = "Courant quittée.";
            _strings["enter_dark_zone"]        = "Zone sombre — la lumière ne fonctionne pas ici.";
            _strings["exit_dark_zone"]         = "Zone sombre quittée.";
            _strings["enter_dream_world"]      = "Entré dans le monde des rêves.";
            _strings["exit_dream_world"]       = "Sorti du monde des rêves.";
            _strings["player_grabbed_ghost"]   = "Attrapé par un fantôme !";
            _strings["player_released_ghost"]  = "Relâché par le fantôme.";

            // ===== AUTO-WALK HANDLER =====
            _strings["auto_walk_hazard"]        = "Danger — {0} ! Auto-marche arrêtée.";
            _strings["hazard_fire"]             = "Feu";
            _strings["hazard_heat"]             = "Chaleur extrême";
            _strings["hazard_darkmatter"]       = "Matière sombre";
            _strings["hazard_electricity"]      = "Électricité";
            _strings["hazard_sandfall"]         = "Chute de sable";
            _strings["hazard_generic"]          = "Zone dangereuse";
            _strings["auto_walk_stuck"]         = "Chemin bloqué. Auto-marche arrêtée.";
            _strings["fluid_water"]             = "Eau";
            _strings["fluid_sand"]              = "Sable en chute";
            _strings["fluid_plasma"]            = "Plasma solaire";
            _strings["fluid_geyser"]            = "Geyser";
            _strings["fluid_tractor"]           = "Faisceau tracteur";
            _strings["auto_walk_out_of_reach"]  = "{0} est hors de portée — trop haut ou trop bas.";
            _strings["auto_walk_on"]            = "Auto-marche vers {0}.";
            _strings["auto_walk_off"]           = "Auto-marche arrêtée.";
            _strings["auto_walk_arrived"]       = "Arrivé à {0}.";
            _strings["auto_walk_cliff"]         = "Falaise — contournement.";
            _strings["auto_walk_steering"]      = "Obstacle — contournement.";
            _strings["auto_walk_jump"]          = "Saut.";

            // ===== PATH GUIDANCE HANDLER =====
            _strings["guidance_on"]       = "Guidage vers {0}.";
            _strings["guidance_off"]      = "Guidage arrêté.";
            _strings["guidance_arrived"]  = "Arrivé à {0}.";
            _strings["auto_walk_no_path"]       = "Aucun chemin trouvé. Auto-marche arrêtée.";
            _strings["auto_walk_danger_steer"]  = "Danger détecté — contournement.";

            // ===== DIALOGUE HANDLER =====

            // ===== SHIP LOG HANDLER =====
            _strings["shiplog_updated"]        = "Journal mis à jour.";
            _strings["shiplog_explored"]       = "Exploré";
            _strings["shiplog_rumored"]        = "Rumeur";
            _strings["shiplog_no_discoveries"] = "Aucune découverte.";
            _strings["shiplog_back_to_map"]    = "Retour à la carte.";
            _strings["shiplog_detective_reveal"] = "Nouvelles découvertes : {0}. Appuie sur E pour passer, puis Q pour le mode carte.";

            // ===== AUTOPILOT =====
            _strings["autopilot_select"]        = "Sélection de destination. Page suivante ou précédente pour choisir. Fin pour confirmer.";
            _strings["autopilot_no_console"]    = "Vous devez être aux commandes du vaisseau.";
            _strings["autopilot_initiated"]     = "Autopilote vers {0}.";
            _strings["autopilot_arrived"]       = "Arrivé à {0}.";
            _strings["autopilot_retro"]         = "Freinage.";
            _strings["autopilot_aborted"]       = "Autopilote annulé.";
            _strings["autopilot_cancelled"]     = "Sélection annulée.";
            _strings["autopilot_already_close"] = "Déjà à proximité de {0}.";
            _strings["autopilot_failed"]        = "Autopilote indisponible.";
            _strings["autopilot_damaged"]       = "Autopilote endommagé.";
            _strings["autopilot_aligning"]           = "Alignement vers la destination.";
            _strings["autopilot_accelerating"]       = "Accélération vers la destination.";
            _strings["autopilot_matching_velocity"]  = "Alignement de vitesse.";
            _strings["autopilot_velocity_matched"]   = "Vitesse alignée.";
            _strings["autopilot_planet_item"]        = "{0} sur {1} : {2}, {3} mètres.";
            _strings["autopilot_planet_item_no_dist"] = "{0} sur {1} : {2}.";

            // ===== SHIP RECALL =====
            _strings["recall_success"]     = "Vaisseau rappelé.";
            _strings["recall_inside"]      = "Vous êtes déjà dans le vaisseau.";
            _strings["recall_destroyed"]   = "Le vaisseau est détruit, impossible de le rappeler.";
            _strings["recall_unavailable"] = "Rappel du vaisseau non disponible.";

            // ===== LOOP TIMER =====
            _strings["timer_remaining"]    = "{0} minutes et {1} secondes restantes.";
            _strings["timer_expired"]      = "Temps écoulé.";
            _strings["timer_unavailable"]  = "Timer non disponible.";

            // Teleport
            _strings["teleport_no_target"]   = "Aucune cible sélectionnée. Scannez d'abord avec Début, puis choisissez avec Page suivante ou précédente.";
            _strings["teleport_too_far"]     = "Cible trop éloignée pour la téléportation.";
            _strings["teleport_not_on_foot"] = "Téléportation disponible uniquement à pied.";
            _strings["teleport_success"]     = "Téléporté vers {0}.";

            // ===== SHIP LOG READER =====
            _strings["logreader_open"]         = "Journal de bord. {0} planètes.";
            _strings["logreader_closed"]       = "Journal fermé.";
            _strings["logreader_planet"]       = "{0}, {1} entrées";
            _strings["logreader_entry"]        = "{0}, {1}, {2} faits";
            _strings["logreader_no_entries"]   = "Aucune découverte dans le journal.";
            _strings["logreader_no_facts"]     = "Aucun fait disponible.";
            _strings["logreader_back_planets"] = "Retour aux planètes.";
            _strings["logreader_back_entries"] = "Retour aux entrées de {0}.";
            _strings["logreader_unavailable"]  = "Journal de bord indisponible.";

            // ===== GHOST MATTER HANDLER =====
            _strings["ghost_matter_near"]  = "Attention — matière fantôme à proximité !";
            _strings["ghost_matter_clear"] = "Zone dégagée.";

            // ===== RESOURCE MONITOR =====
            _strings["gauge_health"]    = "Santé à {0} pourcent.";
            _strings["gauge_oxygen"]    = "Oxygène à {0} pourcent.";
            _strings["gauge_jetpack"]   = "Carburant de jetpack à {0} pourcent.";
            _strings["gauge_ship_fuel"] = "Carburant du vaisseau à {0} pourcent.";

            // ===== ON-DEMAND STATUS (H / J / K) =====
            _strings["status_unavailable"]      = "État indisponible.";

            // H — Personal
            _strings["status_health"]           = "Santé {0} pourcent";
            _strings["status_oxygen_min"]       = "Oxygène {0} minutes {1} secondes";
            _strings["status_oxygen_sec"]       = "Oxygène {0} secondes";
            _strings["status_jetpack"]          = "Jetpack {0} pourcent";
            _strings["status_boost"]            = "Boost {0} pourcent";
            _strings["status_suit_punctured"]   = "Combinaison percée";

            // J — Ship
            _strings["status_ship_unavailable"] = "Vaisseau indisponible.";
            _strings["status_ship_fuel"]        = "Carburant vaisseau {0} pourcent";
            _strings["status_ship_oxygen"]      = "Oxygène vaisseau {0} pourcent";
            _strings["status_ship_integrity"]   = "Intégrité coque {0} pourcent";
            _strings["status_ship_hull_breach"] = "Coque percée";
            _strings["status_ship_ok"]          = "Aucun dégât";
            _strings["status_ship_reactor"]     = "Réacteur critique";
            _strings["status_ship_electrical"]  = "Panne électrique";

            // K — Environment
            _strings["status_hazard"]           = "Danger : {0}, {1} dégâts par seconde";
            _strings["status_no_hazard"]        = "Aucun danger";
            _strings["status_zero_g"]           = "Gravité zéro";
            _strings["status_underwater"]       = "Sous l'eau";
            _strings["hazard_ghost_matter"]     = "Matière fantôme";
            _strings["hazard_fire"]             = "Feu";
            _strings["hazard_heat"]             = "Chaleur";
            _strings["hazard_electricity"]      = "Électricité";
            _strings["hazard_sand"]             = "Sable";
            _strings["hazard_unknown"]          = "Inconnu";

            // ===== ACCESSIBILITY MENU — items =====
            _strings["menu_item_gauge"]         = "Avertissements de ressources";
            _strings["menu_item_guidance"]      = "Guidage audio par tics";
            _strings["menu_item_meditation"]    = "Méditation débloquée dès le départ";
            _strings["menu_item_ghostmatterprotection"] = "Protection matière fantôme";
            _strings["menu_item_shiprecall"]      = "Rappel du vaisseau";
            _strings["menu_item_autopilot"]      = "Autopilote vers planète";
            _strings["menu_item_peacefulghosts"] = "Fantômes pacifiques (DLC)";

            // ===== SHIP PILOT HANDLER =====
            _strings["pilot_speed_stationary"]  = "Immobile";
            _strings["pilot_speed_slow"]        = "Lent";
            _strings["pilot_speed_moderate"]    = "Modéré";
            _strings["pilot_speed_fast"]        = "Rapide";
            _strings["pilot_speed_very_fast"]   = "Très rapide";
            _strings["pilot_speed"]             = "Vitesse : {0}, {1} m/s.";
            _strings["pilot_altitude"]          = "Altitude : {0} mètres.";
            _strings["pilot_approach_warning"]  = "Attention — approche à {0} m/s.";
            _strings["pilot_approach_danger"]   = "Danger — approche rapide à {0} m/s !";
            _strings["pilot_liftoff"]           = "Décollage.";
            _strings["pilot_approach_body"]     = "Approche de {0}.";
            _strings["pilot_lost_target"]       = "Cible perdue.";
            _strings["pilot_hull_damaged"]      = "Coque endommagée : {0}.";
            _strings["pilot_component_damaged"] = "Composant endommagé : {0}.";
            _strings["pilot_altimeter_on"]      = "Altimètre activé.";
            _strings["pilot_altimeter_off"]     = "Altimètre désactivé.";

            // Ship part names (hull sections)
            _strings["pilot_part_top"]          = "Dessus";
            _strings["pilot_part_forward"]      = "Avant";
            _strings["pilot_part_port"]         = "Bâbord";
            _strings["pilot_part_landing"]      = "Train d'atterrissage";
            _strings["pilot_part_starboard"]    = "Tribord";
            _strings["pilot_part_aft"]          = "Arrière";

            // Ship part names (components)
            _strings["pilot_part_autopilot"]    = "Autopilote";
            _strings["pilot_part_fuel"]         = "Réservoir de carburant";
            _strings["pilot_part_gravity"]      = "Générateur de gravité";
            _strings["pilot_part_lights"]       = "Éclairage";
            _strings["pilot_part_camera"]       = "Caméra d'atterrissage";
            _strings["pilot_part_left_thrust"]  = "Propulseur gauche";
            _strings["pilot_part_electric"]     = "Système électrique";
            _strings["pilot_part_o2"]           = "Réserve d'oxygène";
            _strings["pilot_part_reactor"]      = "Réacteur";
            _strings["pilot_part_right_thrust"] = "Propulseur droit";

            // On-demand status (I)
            _strings["pilot_not_at_console"]       = "Vous devez être aux commandes du vaisseau.";
            _strings["pilot_unavailable"]          = "Données de vol non disponibles.";
            _strings["pilot_status_speed"]         = "Vitesse : {0} m/s, {1}.";
            _strings["pilot_status_near"]          = "Près de {0}.";
            _strings["pilot_status_altitude"]      = "Altitude : {0} mètres.";
            _strings["pilot_status_hull_breach"]   = "Brèche dans la coque !";
            _strings["pilot_status_damaged"]       = "Coque à {0} pourcent.";
            _strings["pilot_status_no_damage"]     = "Aucun dommage.";
            _strings["pilot_status_reactor_critical"] = "Réacteur critique !";
            _strings["pilot_status_electrical_fail"]  = "Panne électrique !";
            _strings["pilot_status_landed"]        = "Vaisseau posé.";

            _strings["menu_item_nvdadirect"]    = "API vocale NVDA directe";

            // ===== SIGNALSCOPE HANDLER =====
            _strings["scope_equipped"]             = "Scope de signal : {0}.";
            _strings["scope_frequency"]            = "Fréquence : {0}.";
            _strings["scope_signal_detected"]      = "Signal détecté : {0}, {1}.";
            _strings["scope_signal_detected_dist"] = "Signal détecté : {0}, {1}, {2} mètres.";
            _strings["scope_signal_lost"]          = "Signal perdu.";
            _strings["scope_signal_identified"]    = "Signal identifié : {0} !";
            _strings["scope_strength"]             = "Puissance : {0}.";
            _strings["scope_strength_dist"]        = "Puissance : {0}, {1} mètres.";
            _strings["scope_unknown_signal"]       = "Signal inconnu";

            // Strength tier descriptions
            _strings["scope_str_very_weak"]  = "Très faible";
            _strings["scope_str_weak"]       = "Faible";
            _strings["scope_str_moderate"]   = "Modéré";
            _strings["scope_str_strong"]     = "Fort";
            _strings["scope_str_maximum"]    = "Maximum";

            // Manual status (U)
            _strings["scope_not_equipped"]     = "Le scope de signal n'est pas équipé.";
            _strings["scope_status_no_signal"] = "Fréquence {0}. Aucun signal détecté.";
            _strings["scope_status_full"]      = "Fréquence {0}. {1}, {2}, {3} mètres, {4} degrés.";
            _strings["scope_status_partial"]   = "Fréquence {0}. {1}, {2}, {3} degrés.";

            // ===== NOMAI TEXT HANDLER =====
            _strings["nomai_root"]  = "Message :";
            _strings["nomai_reply"] = "Réponse :";
            _strings["nomai_page"]  = "Page {0} sur {1}.";

            _strings["menu_item_collision"]     = "Bip de collision";
            _strings["menu_item_autowalk"]      = "Auto-marche";
            _strings["menu_item_proximity"]     = "Annonce de proximité";
            _strings["proximity_nearby"]        = "{0}.";

            // ===== BEACON HANDLER =====
            _strings["beacon_on"]      = "Balise activée.";
            _strings["beacon_off"]     = "Balise désactivée.";
            _strings["beacon_lost"]    = "Cible de balise perdue.";
            _strings["beacon_muted"]   = "Balise silencieuse.";
            _strings["beacon_unmuted"] = "Balise reprise.";

            // ===== ACCESSIBILITY MENU =====
            _strings["menu_open"]   = "Réglages ouverts. Flèches ou Page pour naviguer. Entrée pour activer ou désactiver. F6 pour fermer.";
            _strings["menu_closed"] = "Réglages sauvegardés.";
            _strings["menu_cancel"] = "Réglages annulés.";
            _strings["cheats_unlocked"] = "Options avancées déverrouillées.";
            _strings["menu_item_beacon"]     = "Balise audio";
            _strings["menu_item_navigation"] = "Navigation";
            _strings["menu_item_status"]    = "{0} : {1}";
            _strings["menu_controls_hint"] = "Naviguer : flèches. Confirmer : {0}. Retour : {1}.";

            // ===== BUTTON LABELS (InputHelper) =====
            // Keyboard
            _strings["btn_enter"]       = "Entrée";
            _strings["btn_space"]       = "Espace";
            _strings["btn_escape"]      = "Échap";
            _strings["btn_backspace"]   = "Retour arrière";
            _strings["btn_delete"]      = "Suppr";
            _strings["btn_up"]          = "Haut";
            _strings["btn_down"]        = "Bas";
            _strings["btn_left"]        = "Gauche";
            _strings["btn_right"]       = "Droite";
            // Xbox
            _strings["btn_xbox_view"]   = "Vue";
            // PlayStation
            _strings["btn_ps_cross"]    = "Croix";
            _strings["btn_ps_circle"]   = "Rond";
            _strings["btn_ps_square"]   = "Carré";
            _strings["btn_ps_share"]    = "Partage";
            _strings["btn_ps_create"]   = "Créer";
            _strings["btn_ps_touchpad"] = "Pavé tactile";
            // D-Pad (shared Xbox/PS)
            _strings["btn_dpad_up"]     = "Croix haut";
            _strings["btn_dpad_down"]   = "Croix bas";
            _strings["btn_dpad_left"]   = "Croix gauche";
            _strings["btn_dpad_right"]  = "Croix droite";

            // ===== PROMPT FORMATTING =====
            _strings["prompt_and"]             = " et ";
            _strings["prompt_button_single"]   = "touche";
            _strings["prompt_button_plural"]   = "touches";

            // ===== MISC =====
            _strings["backend_speech"]         = "Backend vocal : {0}";
            _strings["autowalk_patch_failed"]  = "Auto-marche : patch non appliqué.";
            _strings["nomai_init_error"]       = "Lecture des textes nomaï indisponible.";
        }

        #endregion

        #region English Strings

        private static void InitializeKeyLabelsEnglish()
        {
            _keyLabels["CONFIRM"]    = "Confirm";
            _keyLabels["CANCEL"]     = "Cancel";
            _keyLabels["INTERACT"]   = "Interact";
            _keyLabels["JUMP"]       = "Jump";
            _keyLabels["ENTER"]      = "Enter";
            _keyLabels["BACK"]       = "Back";
            _keyLabels["PAUSE"]      = "Pause";
            _keyLabels["MAP"]        = "Map";
            _keyLabels["SUIT"]       = "Suit";
            _keyLabels["FLASHLIGHT"] = "Flashlight";
        }

        private static void InitializeEnglish()
        {
            // ===== GENERAL =====
            _strings["mod_loaded"]  = "Outer Wilds Access loaded. Press F1 for help.";
            _strings["debug_on"]    = "Debug mode enabled.";
            _strings["debug_off"]   = "Debug mode disabled.";
            _strings["mod_disabled"] = "Outer Wilds Access disabled.";
            _strings["mod_enabled"]  = "Outer Wilds Access enabled.";

            // ===== HELP MENU (F1) =====
            _strings["help_open"]        = "Help opened. Arrows to navigate. Enter to open a category. Backspace to go back. Escape to close.";
            _strings["help_close"]       = "Help closed.";
            _strings["help_category"]    = "{0}, {1} shortcuts.";
            _strings["help_cat_entered"] = "{0}, {1} shortcuts.";
            _strings["help_item"]        = "{0}: {1}";

            // Category names
            _strings["help_cat_general"]    = "General";
            _strings["help_cat_navigation"] = "Navigation";
            _strings["help_cat_status"]     = "Status";
            _strings["help_cat_ship"]       = "Ship";
            _strings["help_cat_tools"]      = "Tools";
            _strings["help_cat_settings"]   = "Settings";

            // Key names
            _strings["help_key_f1"]         = "F1";
            _strings["help_key_f2"]         = "F2";
            _strings["help_key_f3"]         = "F3";
            _strings["help_key_f4"]         = "F4";
            _strings["help_key_f5"]         = "F5";
            _strings["help_key_f6"]         = "F6";
            _strings["help_key_f12"]        = "F12";
            _strings["help_key_delete"]     = "Delete";
            _strings["help_key_backspace"]  = "Backspace";
            _strings["help_key_home"]       = "Home";
            _strings["help_key_end"]        = "End";
            _strings["help_key_pageupdown"] = "Page Up and Page Down";
            _strings["help_key_altpage"]    = "Alt + Page Up and Page Down";
            _strings["help_key_g"]          = "G";
            _strings["help_key_h"]          = "H";
            _strings["help_key_i"]          = "I";
            _strings["help_key_j"]          = "J";
            _strings["help_key_k"]          = "K";
            _strings["help_key_l"]          = "L";
            _strings["help_key_o"]          = "O";
            _strings["help_key_t"]          = "T";
            _strings["help_key_u"]          = "U";

            // Descriptions
            _strings["help_desc_f1"]        = "Help — opens this menu";
            _strings["help_desc_f2"]        = "Time remaining before the supernova";
            _strings["help_desc_f3"]        = "Recall your ship above you";
            _strings["help_desc_f4"]        = "Ship log — browse your discoveries";
            _strings["help_desc_f5"]        = "Disable or re-enable the mod";
            _strings["help_desc_f6"]        = "Open settings menu";
            _strings["help_desc_f12"]       = "Toggle debug mode";
            _strings["help_desc_delete"]    = "Repeat last announcement";
            _strings["help_desc_backspace"] = "Mute or unmute the audio beacon";
            _strings["help_desc_home_nav"]  = "Scan nearby objects";
            _strings["help_desc_pageupdown_nav"]  = "Cycle through scanned objects";
            _strings["help_desc_altpage"]   = "Switch navigation category";
            _strings["help_desc_end_nav"]   = "Distance and direction to target";
            _strings["help_desc_l"]         = "Detailed position — planet, zone and nearby location";
            _strings["help_desc_g"]         = "Audio guidance to target using tone cues";
            _strings["help_desc_o"]         = "Auto-walk to target";
            _strings["help_desc_t"]         = "Teleport to target — same planet, 500 meters max";
            _strings["help_desc_h"]         = "Personal status — health, oxygen, jetpack, boost, suit";
            _strings["help_desc_j"]         = "Ship status — fuel, oxygen, hull, damage";
            _strings["help_desc_k"]         = "Environment — active hazards, gravity, water";
            _strings["help_desc_i"]         = "Flight telemetry — speed, altitude, damage";
            _strings["help_desc_home_pilot"]       = "Select autopilot destination — at ship controls";
            _strings["help_desc_pageupdown_pilot"] = "Cycle planets — at ship controls";
            _strings["help_desc_end_pilot"]        = "Launch autopilot to destination — at ship controls";
            _strings["help_desc_u"]         = "Signal scope status — frequency and detected signal";

            // ===== MENU HANDLER =====
            _strings["toggle_on"]       = "Enabled";
            _strings["toggle_off"]      = "Disabled";
            _strings["slider_value"]    = "{0} out of 10";
            _strings["rebinding_enter"] = "Press a key to assign.";
            _strings["rebinding_done"]  = "Key assigned: {0}.";
            _strings["rebinding_cancel"]= "Cancelled.";

            // ===== STATE HANDLER =====
            // Death causes
            _strings["death_default"]          = "Dead.";
            _strings["death_impact"]           = "Killed on impact.";
            _strings["death_asphyxiation"]     = "Asphyxiation — out of oxygen.";
            _strings["death_energy"]           = "Electrocuted.";
            _strings["death_supernova"]        = "Consumed by the supernova.";
            _strings["death_digestion"]        = "Digested by the plant.";
            _strings["death_bigbang"]          = "End of loop — the sun explodes.";
            _strings["death_crushed"]          = "Crushed.";
            _strings["death_meditation"]       = "Meditation — skipping to next cycle.";
            _strings["death_timeloop"]         = "End of time loop.";
            _strings["death_lava"]             = "Killed by lava.";
            _strings["death_blackhole"]        = "Sucked into a black hole.";
            _strings["death_dream"]            = "Died in the dream.";
            _strings["death_dreamexplosion"]   = "Explosion in the dream.";
            _strings["death_crushedbyelevator"] = "Crushed by the elevator.";

            // Respawn / cycle
            _strings["player_respawn"]         = "New cycle. You're back at the ship.";

            // Ship
            _strings["enter_ship"]             = "Inside the ship.";
            _strings["exit_ship"]              = "Left the ship.";
            _strings["enter_flight_console"]   = "At ship controls.";
            _strings["exit_flight_console"]    = "Left ship controls.";
            _strings["ship_hull_breach"]       = "Warning — ship hull breached!";
            _strings["enter_ship_computer"]    = "Ship log opened.";
            _strings["exit_ship_computer"]     = "Ship log closed.";
            _strings["enter_landing_view"]     = "Landing camera activated.";
            _strings["exit_landing_view"]      = "Landing camera deactivated.";

            // Equipment
            _strings["suit_on"]                = "Suit equipped.";
            _strings["suit_off"]               = "Suit removed.";
            _strings["flashlight_on"]          = "Flashlight on.";
            _strings["flashlight_off"]         = "Flashlight off.";
            _strings["equip_signalscope"]      = "Signalscope equipped.";
            _strings["unequip_signalscope"]    = "Signalscope stowed.";
            _strings["equip_translator"]       = "Translator equipped.";
            _strings["unequip_translator"]     = "Translator stowed.";

            // Map
            _strings["enter_map"]              = "Solar system map opened.";
            _strings["exit_map"]               = "Map closed.";

            // Time
            _strings["fast_forward_start"]     = "Fast forwarding time.";
            _strings["fast_forward_end"]       = "Normal time.";

            // Conversation
            _strings["enter_conversation"]     = "Dialogue.";
            _strings["exit_conversation"]      = "Dialogue ended.";

            // Signalscope
            _strings["enter_signalscope"]      = "Signalscope activated.";
            _strings["exit_signalscope"]       = "Signalscope deactivated.";

            // ===== NAVIGATION HANDLER =====
            _strings["nav_ship"]          = "Ship";
            _strings["nav_model_rocket"]  = "Model rocket";
            _strings["nav_nothing_found"] = "No objects nearby.";
            _strings["nav_scan_first"]    = "{0} object(s) found. {1}, {2} meters.";
            _strings["nav_item"]          = "{0} of {1}: {2}, {3} meters.";
            _strings["nav_stale"]         = "List outdated — press Home to refresh.";
            _strings["nav_no_scan"]       = "Scan first with the Home key.";
            _strings["nav_no_target"]     = "Select an object with Page Down, then press End.";
            _strings["nav_target_lost"]    = "Target lost.";
            _strings["nav_target_cleared"] = "Target cleared.";
            _strings["nav_navigate"]      = "{0}: {1}, {2}m";
            _strings["nav_interact_hint"] = "Press {0} to interact.";
            _strings["nav_north"]         = "ahead";
            _strings["nav_south"]         = "behind";
            _strings["nav_east"]          = "to the right";
            _strings["nav_west"]          = "to the left";
            _strings["nav_up"]            = "above";
            _strings["nav_down"]          = "below";
            _strings["nav_here"]          = "here";
            _strings["nav_live"]          = "{0}, {1}m";

            // ===== CATEGORY NAVIGATION (Alt+PageUp/Down) =====
            _strings["nav_cat_ship"]           = "Ship";
            _strings["nav_cat_npcs"]           = "Characters";
            _strings["nav_cat_interactables"]  = "Interactables";
            _strings["nav_cat_nomai"]          = "Nomai texts";
            _strings["nav_cat_locations"]      = "Locations";
            _strings["nav_cat_signs"]          = "Signs";
            _strings["nav_cat_announce"]       = "{0}: {1} result(s). {2}, {3} meters.";
            _strings["nav_cat_empty"]          = "{0}: no results.";

            // Nomai text labels (object scan)
            _strings["nav_nomai_wall"]       = "Nomai wall text";
            _strings["nav_nomai_computer"]   = "Nomai computer";

            // Campfire labels
            _strings["nav_campfire"]            = "Campfire";
            _strings["nav_campfire_lit"]         = "lit";
            _strings["nav_campfire_smoldering"]  = "smoldering";
            _strings["nav_campfire_unlit"]       = "unlit";

            // Sub-sector translations
            _strings["sector_village"]             = "Village";
            _strings["sector_zerogcave"]           = "Zero-G Cave";
            _strings["sector_observatory"]         = "Observatory";
            _strings["sector_museum"]              = "Museum";
            _strings["sector_north_pole"]          = "North Pole";
            _strings["sector_south_pole"]          = "South Pole";
            _strings["sector_crossroads"]          = "Crossroads";
            _strings["sector_canyons"]             = "Canyons";
            _strings["sector_anglerfish"]          = "Anglerfish";
            _strings["sector_oldsettle"]           = "Old Settlement";
            _strings["sector_gravitycannon"]       = "Gravity Cannon";
            _strings["sector_towerofknowledge"]    = "Tower of Knowledge";
            _strings["sector_blackholeforge"]      = "Black Hole Forge";
            _strings["sector_hangingcity"]         = "Hanging City";
            _strings["sector_constructionyard"]    = "Construction Yard";
            _strings["sector_escape_pod"]          = "Escape Pod";
            _strings["sector_thlanding"]           = "Landing Zone";
            _strings["sector_geyser"]              = "Geyser";
            _strings["sector_undergroundlake"]     = "Underground Lake";
            _strings["sector_quantumgrove"]        = "Quantum Grove";
            _strings["sector_quantumcaves"]        = "Quantum Caves";

            // ===== LOCATION HANDLER =====
            _strings["location_enter"]         = "Arrived: {0}.";
            _strings["location_current"]       = "Position: {0}.";
            _strings["location_space"]         = "Orbiting in space.";
            _strings["location_unknown"]       = "Position unknown.";
            _strings["location_near"]          = "near {0}";

            // Planet / zone names (official English names)
            _strings["loc_sun"]                = "the Sun";
            _strings["loc_ash_twin"]           = "Ash Twin";
            _strings["loc_ember_twin"]         = "Ember Twin";
            _strings["loc_hourglass_twins"]    = "Hourglass Twins";
            _strings["loc_timber_hearth"]      = "Timber Hearth";
            _strings["loc_brittle_hollow"]     = "Brittle Hollow";
            _strings["loc_giants_deep"]        = "Giant's Deep";
            _strings["loc_dark_bramble"]       = "Dark Bramble";
            _strings["loc_comet"]              = "The Interloper";
            _strings["loc_quantum_moon"]       = "Quantum Moon";
            _strings["loc_timber_moon"]        = "Attlerock";
            _strings["loc_volcanic_moon"]      = "Hollow's Lantern";
            _strings["loc_bramble_dimension"]  = "Bramble Dimension";
            _strings["loc_probe_cannon"]       = "Orbital Probe Cannon";
            _strings["loc_eye"]                = "the Eye of the Universe";
            _strings["loc_sun_station"]        = "Sun Station";
            _strings["loc_white_hole"]         = "White Hole";
            _strings["loc_time_loop_device"]   = "Time Loop Device";
            _strings["loc_vessel"]             = "the Nomai Vessel";
            _strings["loc_vessel_dimension"]   = "Nomai Vessel Dimension";
            _strings["loc_dream_world"]        = "Dream World";
            _strings["loc_invisible_planet"]   = "Invisible Planet";

            // Environment
            _strings["camera_enter_water"]     = "Camera underwater.";
            _strings["attach_to_point"]        = "Attached to a surface.";
            _strings["detach_from_point"]      = "Detached from surface.";
            _strings["enter_undertow"]         = "Caught in undertow.";
            _strings["exit_undertow"]          = "Left the undertow.";
            _strings["enter_dark_zone"]        = "Dark zone — light doesn't work here.";
            _strings["exit_dark_zone"]         = "Left dark zone.";
            _strings["enter_dream_world"]      = "Entered the dream world.";
            _strings["exit_dream_world"]       = "Left the dream world.";
            _strings["player_grabbed_ghost"]   = "Grabbed by a ghost!";
            _strings["player_released_ghost"]  = "Released by the ghost.";

            // ===== AUTO-WALK HANDLER =====
            _strings["auto_walk_hazard"]        = "Danger — {0}! Auto-walk stopped.";
            _strings["hazard_fire"]             = "Fire";
            _strings["hazard_heat"]             = "Extreme heat";
            _strings["hazard_darkmatter"]       = "Dark matter";
            _strings["hazard_electricity"]      = "Electricity";
            _strings["hazard_sandfall"]         = "Sandfall";
            _strings["hazard_generic"]          = "Hazardous zone";
            _strings["auto_walk_stuck"]         = "Path blocked. Auto-walk stopped.";
            _strings["fluid_water"]             = "Water";
            _strings["fluid_sand"]              = "Falling sand";
            _strings["fluid_plasma"]            = "Solar plasma";
            _strings["fluid_geyser"]            = "Geyser";
            _strings["fluid_tractor"]           = "Tractor beam";
            _strings["auto_walk_out_of_reach"]  = "{0} is out of reach — too high or too low.";
            _strings["auto_walk_on"]            = "Auto-walking to {0}.";
            _strings["auto_walk_off"]           = "Auto-walk stopped.";
            _strings["auto_walk_arrived"]       = "Arrived at {0}.";
            _strings["auto_walk_cliff"]         = "Cliff — rerouting.";
            _strings["auto_walk_steering"]      = "Obstacle — rerouting.";
            _strings["auto_walk_jump"]          = "Jump.";

            // ===== PATH GUIDANCE HANDLER =====
            _strings["guidance_on"]       = "Guiding to {0}.";
            _strings["guidance_off"]      = "Guidance stopped.";
            _strings["guidance_arrived"]  = "Arrived at {0}.";
            _strings["auto_walk_no_path"]       = "No path found. Auto-walk stopped.";
            _strings["auto_walk_danger_steer"]  = "Danger detected — rerouting.";

            // ===== DIALOGUE HANDLER =====

            // ===== SHIP LOG HANDLER =====
            _strings["shiplog_updated"]        = "Log updated.";
            _strings["shiplog_explored"]       = "Explored";
            _strings["shiplog_rumored"]        = "Rumored";
            _strings["shiplog_no_discoveries"] = "No discoveries.";
            _strings["shiplog_back_to_map"]    = "Back to map.";
            _strings["shiplog_detective_reveal"] = "New discoveries: {0}. Press E to continue, then Q for map mode.";

            // ===== AUTOPILOT =====
            _strings["autopilot_select"]        = "Destination selection. Page Up or Down to choose. End to confirm.";
            _strings["autopilot_no_console"]    = "You must be at ship controls.";
            _strings["autopilot_initiated"]     = "Autopilot to {0}.";
            _strings["autopilot_arrived"]       = "Arrived at {0}.";
            _strings["autopilot_retro"]         = "Braking.";
            _strings["autopilot_aborted"]       = "Autopilot aborted.";
            _strings["autopilot_cancelled"]     = "Selection cancelled.";
            _strings["autopilot_already_close"] = "Already near {0}.";
            _strings["autopilot_failed"]        = "Autopilot unavailable.";
            _strings["autopilot_damaged"]       = "Autopilot damaged.";
            _strings["autopilot_aligning"]           = "Aligning to destination.";
            _strings["autopilot_accelerating"]       = "Accelerating to destination.";
            _strings["autopilot_matching_velocity"]  = "Matching velocity.";
            _strings["autopilot_velocity_matched"]   = "Velocity matched.";
            _strings["autopilot_planet_item"]        = "{0} of {1}: {2}, {3} meters.";
            _strings["autopilot_planet_item_no_dist"] = "{0} of {1}: {2}.";

            // ===== SHIP RECALL =====
            _strings["recall_success"]     = "Ship recalled.";
            _strings["recall_inside"]      = "You're already in the ship.";
            _strings["recall_destroyed"]   = "Ship is destroyed, cannot recall.";
            _strings["recall_unavailable"] = "Ship recall unavailable.";

            // ===== LOOP TIMER =====
            _strings["timer_remaining"]    = "{0} minutes and {1} seconds remaining.";
            _strings["timer_expired"]      = "Time expired.";
            _strings["timer_unavailable"]  = "Timer unavailable.";

            // Teleport
            _strings["teleport_no_target"]   = "No target selected. Scan first with Home, then choose with Page Up or Down.";
            _strings["teleport_too_far"]     = "Target too far for teleportation.";
            _strings["teleport_not_on_foot"] = "Teleportation only available on foot.";
            _strings["teleport_success"]     = "Teleported to {0}.";

            // ===== SHIP LOG READER =====
            _strings["logreader_open"]         = "Ship log. {0} planets.";
            _strings["logreader_closed"]       = "Log closed.";
            _strings["logreader_planet"]       = "{0}, {1} entries";
            _strings["logreader_entry"]        = "{0}, {1}, {2} facts";
            _strings["logreader_no_entries"]   = "No discoveries in the log.";
            _strings["logreader_no_facts"]     = "No facts available.";
            _strings["logreader_back_planets"] = "Back to planets.";
            _strings["logreader_back_entries"] = "Back to entries for {0}.";
            _strings["logreader_unavailable"]  = "Ship log unavailable.";

            // ===== GHOST MATTER HANDLER =====
            _strings["ghost_matter_near"]  = "Warning — ghost matter nearby!";
            _strings["ghost_matter_clear"] = "Area clear.";

            // ===== RESOURCE MONITOR =====
            _strings["gauge_health"]    = "Health at {0} percent.";
            _strings["gauge_oxygen"]    = "Oxygen at {0} percent.";
            _strings["gauge_jetpack"]   = "Jetpack fuel at {0} percent.";
            _strings["gauge_ship_fuel"] = "Ship fuel at {0} percent.";

            // ===== ON-DEMAND STATUS (H / J / K) =====
            _strings["status_unavailable"]      = "Status unavailable.";

            // H — Personal
            _strings["status_health"]           = "Health {0} percent";
            _strings["status_oxygen_min"]       = "Oxygen {0} minutes {1} seconds";
            _strings["status_oxygen_sec"]       = "Oxygen {0} seconds";
            _strings["status_jetpack"]          = "Jetpack {0} percent";
            _strings["status_boost"]            = "Boost {0} percent";
            _strings["status_suit_punctured"]   = "Suit punctured";

            // J — Ship
            _strings["status_ship_unavailable"] = "Ship unavailable.";
            _strings["status_ship_fuel"]        = "Ship fuel {0} percent";
            _strings["status_ship_oxygen"]      = "Ship oxygen {0} percent";
            _strings["status_ship_integrity"]   = "Hull integrity {0} percent";
            _strings["status_ship_hull_breach"] = "Hull breached";
            _strings["status_ship_ok"]          = "No damage";
            _strings["status_ship_reactor"]     = "Reactor critical";
            _strings["status_ship_electrical"]  = "Electrical failure";

            // K — Environment
            _strings["status_hazard"]           = "Danger: {0}, {1} damage per second";
            _strings["status_no_hazard"]        = "No hazards";
            _strings["status_zero_g"]           = "Zero gravity";
            _strings["status_underwater"]       = "Underwater";
            _strings["hazard_ghost_matter"]     = "Ghost matter";
            _strings["hazard_fire"]             = "Fire";
            _strings["hazard_heat"]             = "Heat";
            _strings["hazard_electricity"]      = "Electricity";
            _strings["hazard_sand"]             = "Sand";
            _strings["hazard_unknown"]          = "Unknown";

            // ===== ACCESSIBILITY MENU — items =====
            _strings["menu_item_gauge"]         = "Resource warnings";
            _strings["menu_item_guidance"]      = "Audio tone guidance";
            _strings["menu_item_meditation"]    = "Meditation unlocked from start";
            _strings["menu_item_ghostmatterprotection"] = "Ghost matter protection";
            _strings["menu_item_shiprecall"]      = "Ship recall";
            _strings["menu_item_autopilot"]      = "Planet autopilot";
            _strings["menu_item_peacefulghosts"] = "Peaceful ghosts (DLC)";

            // ===== SHIP PILOT HANDLER =====
            _strings["pilot_speed_stationary"]  = "Stationary";
            _strings["pilot_speed_slow"]        = "Slow";
            _strings["pilot_speed_moderate"]    = "Moderate";
            _strings["pilot_speed_fast"]        = "Fast";
            _strings["pilot_speed_very_fast"]   = "Very fast";
            _strings["pilot_speed"]             = "Speed: {0}, {1} m/s.";
            _strings["pilot_altitude"]          = "Altitude: {0} meters.";
            _strings["pilot_approach_warning"]  = "Warning — approaching at {0} m/s.";
            _strings["pilot_approach_danger"]   = "Danger — rapid approach at {0} m/s!";
            _strings["pilot_liftoff"]           = "Liftoff.";
            _strings["pilot_approach_body"]     = "Approaching {0}.";
            _strings["pilot_lost_target"]       = "Target lost.";
            _strings["pilot_hull_damaged"]      = "Hull damaged: {0}.";
            _strings["pilot_component_damaged"] = "Component damaged: {0}.";
            _strings["pilot_altimeter_on"]      = "Altimeter on.";
            _strings["pilot_altimeter_off"]     = "Altimeter off.";

            // Ship part names (hull sections)
            _strings["pilot_part_top"]          = "Top";
            _strings["pilot_part_forward"]      = "Forward";
            _strings["pilot_part_port"]         = "Port";
            _strings["pilot_part_landing"]      = "Landing gear";
            _strings["pilot_part_starboard"]    = "Starboard";
            _strings["pilot_part_aft"]          = "Aft";

            // Ship part names (components)
            _strings["pilot_part_autopilot"]    = "Autopilot";
            _strings["pilot_part_fuel"]         = "Fuel tank";
            _strings["pilot_part_gravity"]      = "Gravity generator";
            _strings["pilot_part_lights"]       = "Lights";
            _strings["pilot_part_camera"]       = "Landing camera";
            _strings["pilot_part_left_thrust"]  = "Left thruster";
            _strings["pilot_part_electric"]     = "Electrical system";
            _strings["pilot_part_o2"]           = "Oxygen reserve";
            _strings["pilot_part_reactor"]      = "Reactor";
            _strings["pilot_part_right_thrust"] = "Right thruster";

            // On-demand status (I)
            _strings["pilot_not_at_console"]       = "You must be at ship controls.";
            _strings["pilot_unavailable"]          = "Flight data unavailable.";
            _strings["pilot_status_speed"]         = "Speed: {0} m/s, {1}.";
            _strings["pilot_status_near"]          = "Near {0}.";
            _strings["pilot_status_altitude"]      = "Altitude: {0} meters.";
            _strings["pilot_status_hull_breach"]   = "Hull breach!";
            _strings["pilot_status_damaged"]       = "Hull at {0} percent.";
            _strings["pilot_status_no_damage"]     = "No damage.";
            _strings["pilot_status_reactor_critical"] = "Reactor critical!";
            _strings["pilot_status_electrical_fail"]  = "Electrical failure!";
            _strings["pilot_status_landed"]        = "Ship landed.";

            _strings["menu_item_nvdadirect"]    = "NVDA direct speech API";

            // ===== SIGNALSCOPE HANDLER =====
            _strings["scope_equipped"]             = "Signalscope: {0}.";
            _strings["scope_frequency"]            = "Frequency: {0}.";
            _strings["scope_signal_detected"]      = "Signal detected: {0}, {1}.";
            _strings["scope_signal_detected_dist"] = "Signal detected: {0}, {1}, {2} meters.";
            _strings["scope_signal_lost"]          = "Signal lost.";
            _strings["scope_signal_identified"]    = "Signal identified: {0}!";
            _strings["scope_strength"]             = "Strength: {0}.";
            _strings["scope_strength_dist"]        = "Strength: {0}, {1} meters.";
            _strings["scope_unknown_signal"]       = "Unknown signal";

            // Strength tier descriptions
            _strings["scope_str_very_weak"]  = "Very weak";
            _strings["scope_str_weak"]       = "Weak";
            _strings["scope_str_moderate"]   = "Moderate";
            _strings["scope_str_strong"]     = "Strong";
            _strings["scope_str_maximum"]    = "Maximum";

            // Manual status (U)
            _strings["scope_not_equipped"]     = "Signalscope is not equipped.";
            _strings["scope_status_no_signal"] = "Frequency {0}. No signal detected.";
            _strings["scope_status_full"]      = "Frequency {0}. {1}, {2}, {3} meters, {4} degrees.";
            _strings["scope_status_partial"]   = "Frequency {0}. {1}, {2}, {3} degrees.";

            // ===== NOMAI TEXT HANDLER =====
            _strings["nomai_root"]  = "Message:";
            _strings["nomai_reply"] = "Reply:";
            _strings["nomai_page"]  = "Page {0} of {1}.";

            _strings["menu_item_collision"]     = "Collision beep";
            _strings["menu_item_autowalk"]      = "Auto-walk";
            _strings["menu_item_proximity"]     = "Proximity announcements";
            _strings["proximity_nearby"]        = "{0}.";

            // ===== BEACON HANDLER =====
            _strings["beacon_on"]      = "Beacon activated.";
            _strings["beacon_off"]     = "Beacon deactivated.";
            _strings["beacon_lost"]    = "Beacon target lost.";
            _strings["beacon_muted"]   = "Beacon muted.";
            _strings["beacon_unmuted"] = "Beacon resumed.";

            // ===== ACCESSIBILITY MENU =====
            _strings["menu_open"]   = "Settings opened. Arrows or Page to navigate. Enter to toggle. F6 to close.";
            _strings["menu_closed"] = "Settings saved.";
            _strings["menu_cancel"] = "Settings cancelled.";
            _strings["cheats_unlocked"] = "Advanced options unlocked.";
            _strings["menu_item_beacon"]     = "Audio beacon";
            _strings["menu_item_navigation"] = "Navigation";
            _strings["menu_item_status"]    = "{0}: {1}";
            _strings["menu_controls_hint"] = "Navigate: arrows. Confirm: {0}. Back: {1}.";

            // ===== BUTTON LABELS (InputHelper) =====
            // Keyboard
            _strings["btn_enter"]       = "Enter";
            _strings["btn_space"]       = "Space";
            _strings["btn_escape"]      = "Escape";
            _strings["btn_backspace"]   = "Backspace";
            _strings["btn_delete"]      = "Delete";
            _strings["btn_up"]          = "Up";
            _strings["btn_down"]        = "Down";
            _strings["btn_left"]        = "Left";
            _strings["btn_right"]       = "Right";
            // Xbox
            _strings["btn_xbox_view"]   = "View";
            // PlayStation
            _strings["btn_ps_cross"]    = "Cross";
            _strings["btn_ps_circle"]   = "Circle";
            _strings["btn_ps_square"]   = "Square";
            _strings["btn_ps_share"]    = "Share";
            _strings["btn_ps_create"]   = "Create";
            _strings["btn_ps_touchpad"] = "Touchpad";
            // D-Pad (shared Xbox/PS)
            _strings["btn_dpad_up"]     = "D-Pad Up";
            _strings["btn_dpad_down"]   = "D-Pad Down";
            _strings["btn_dpad_left"]   = "D-Pad Left";
            _strings["btn_dpad_right"]  = "D-Pad Right";

            // ===== PROMPT FORMATTING =====
            _strings["prompt_and"]             = " and ";
            _strings["prompt_button_single"]   = "button";
            _strings["prompt_button_plural"]   = "buttons";

            // ===== MISC =====
            _strings["backend_speech"]         = "Speech backend: {0}";
            _strings["autowalk_patch_failed"]  = "Auto-walk: patch not applied.";
            _strings["nomai_init_error"]       = "Nomai text reading unavailable.";
        }

        #endregion
    }
}
