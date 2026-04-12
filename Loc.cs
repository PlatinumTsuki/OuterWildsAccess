using System;
using System.Collections.Generic;

namespace OuterWildsAccess
{
    /// <summary>
    /// Localization for OuterWildsAccess.
    /// Supports French, German and English. Detects game language automatically.
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
            TextTranslation.Language lang = TextTranslation.Language.ENGLISH;
            try
            {
                lang = TextTranslation.Get().GetLanguage();
            }
            catch
            {
                // TextTranslation not ready — default to English
            }

            if (lang == TextTranslation.Language.FRENCH)
            {
                InitializeFrench();
                InitializeKeyLabelsFrench();
            }
            else if (lang == TextTranslation.Language.GERMAN)
            {
                // German is a full translation; any missing key will fall back
                // to returning the raw key, so we pre-load English first and
                // then overwrite with German. This guarantees EN fallback for
                // any string the translator may have missed.
                InitializeEnglish();
                InitializeKeyLabelsEnglish();
                InitializeGerman();
                InitializeKeyLabelsGerman();
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
            _strings["help_key_m"]          = "B";
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
            _strings["help_desc_m"]         = "Marche automatique vers la cible";
            _strings["help_desc_t"]         = "Téléportation vers la cible — même planète, 500 mètres maximum";
            _strings["help_desc_h"]         = "État personnel — santé, oxygène, jetpack, boost, combinaison";
            _strings["help_desc_j"]         = "État du vaisseau — carburant, oxygène, coque, dégâts";
            _strings["help_desc_k"]         = "Environnement — dangers actifs, gravité, eau";
            _strings["help_desc_i"]         = "Télémétrie de vol — vitesse, altitude, dégâts";
            _strings["help_desc_home_pilot"]       = "Sélectionner une destination pour l'autopilote — aux commandes";
            _strings["help_desc_pageupdown_pilot"] = "Parcourir les planètes — aux commandes";
            _strings["help_desc_end_pilot"]        = "Lancer l'autopilote vers la destination — aux commandes";
            _strings["help_desc_u"]         = "Statut du scope de signal — fréquence et signal détecté";
            _strings["help_key_o"]          = "O";
            _strings["help_desc_o"]         = "Statut du guetteur — distance, ancrage, interférence";

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
            _strings["nav_repair_item"]              = "{0} endommagé(e), intégrité {1} %";
            _strings["repair_focus"]                 = "{0} réparable, intégrité {1} %.";
            _strings["repair_started"]               = "Réparation en cours.";
            _strings["repair_progress"]              = "{0} %.";
            _strings["repair_finished"]              = "Réparation terminée.";
            _strings["repair_interrupted"]           = "Réparation interrompue à {0} %.";
            _strings["repair_part_ShipPartTop"]      = "Coque supérieure";
            _strings["repair_part_ShipPartLanding"]  = "Coque inférieure";
            _strings["repair_part_ShipPartForward"]  = "Coque avant";
            _strings["repair_part_ShipPartAft"]      = "Coque arrière";
            _strings["repair_part_ShipPartPort"]     = "Coque bâbord";
            _strings["repair_part_ShipPartStarboard"] = "Coque tribord";
            _strings["repair_part_ShipPartO2"]       = "Réservoir d'oxygène";
            _strings["repair_part_ShipPartFuel"]     = "Réservoir de carburant";
            _strings["repair_part_ShipPartElectric"] = "Système électrique";
            _strings["repair_part_ShipPartReactor"]  = "Réacteur";
            _strings["repair_part_ShipPartGravity"]  = "Plancher gravitationnel";
            _strings["repair_part_ShipPartAutopilot"] = "Autopilote";
            _strings["repair_part_ShipPartLights"]   = "Phares";
            _strings["repair_part_ShipPartCamera"]   = "Caméra arrière";
            _strings["repair_part_ShipPartLeftThrust"]  = "Propulseur gauche";
            _strings["repair_part_ShipPartRightThrust"] = "Propulseur droit";
            _strings["repair_part_ShipPartUnknown"]  = "Pièce inconnue";
            _strings["nav_model_rocket"]  = "Fusée modèle réduit";
            _strings["nav_nomai_statue"]  = "Statue Nomaï";
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
            _strings["loc_sun"]                = "Soleil";
            _strings["loc_ash_twin"]           = "Sablière rouge";
            _strings["loc_ember_twin"]         = "Sablière noire";
            _strings["loc_hourglass_twins"]    = "Sablières";
            _strings["loc_timber_hearth"]      = "Âtrebois";
            _strings["loc_brittle_hollow"]     = "Cravité";
            _strings["loc_giants_deep"]        = "Léviathé";
            _strings["loc_dark_bramble"]       = "Sombronces";
            _strings["loc_comet"]              = "L'Intrus";
            _strings["loc_quantum_moon"]       = "Lune quantique";
            _strings["loc_timber_moon"]        = "La Rocaille";
            _strings["loc_volcanic_moon"]      = "La Lanterne";
            _strings["loc_bramble_dimension"]  = "Dimension de Sombronces";
            _strings["loc_probe_cannon"]       = "Lance-sondes orbital";
            _strings["loc_eye"]                = "L'Œil de l'univers";
            _strings["loc_sun_station"]        = "Station solaire";
            _strings["loc_white_hole"]         = "Trou blanc";
            _strings["loc_time_loop_device"]   = "Dispositif de boucle temporelle";
            _strings["loc_vessel"]             = "Vaisseau nomaï";
            _strings["loc_vessel_dimension"]   = "Dimension du Vaisseau nomaï";
            _strings["loc_dream_world"]        = "Monde des Rêves";
            _strings["loc_invisible_planet"]   = "L'Étranger";

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
            _strings["auto_walk_unsafe_path"]   = "Chemin non sécurisé, auto-marche annulée.";
            _strings["fluid_water"]             = "Eau";
            _strings["fluid_sand"]              = "Sable en chute";
            _strings["fluid_plasma"]            = "Plasma solaire";
            _strings["fluid_geyser"]            = "Geyser";
            _strings["fluid_tractor"]           = "Faisceau tracteur";
            _strings["auto_walk_wading"]        = "Eau peu profonde — marche en cours.";
            _strings["fluid_deep_water"]        = "Eau profonde";
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
            _strings["autopilot_aligned"]       = "Vaisseau aligné avec la surface.";
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
            _strings["autopilot_planet_item_km"]     = "{0} sur {1} : {2}, {3} kilomètres.";
            _strings["autopilot_planet_item_no_dist"] = "{0} sur {1} : {2}.";

            // ===== MODEL ROCKET =====
            _strings["model_rocket_console_enter"]  = "Console fusée modèle réduit. Fin pour autopilote vers le geyser.";
            _strings["model_rocket_autopilot_on"]    = "Autopilote fusée activé. Vol vers le geyser.";
            _strings["model_rocket_autopilot_off"]   = "Autopilote fusée désactivé.";
            _strings["model_rocket_no_target"]       = "Aucun geyser trouvé.";
            _strings["model_rocket_landed"]          = "Fusée posée sur le geyser !";
            _strings["model_rocket_distance"]        = "{0} mètres.";

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
            _strings["action_inside_ship"]   = "Action impossible à l'intérieur du vaisseau.";
            _strings["teleport_success"]     = "Téléporté vers {0}.";
            _strings["teleport_unsafe"]       = "Zone d'atterrissage dangereuse — téléportation annulée.";
            _strings["teleport_unsafe_water"] = "{0} est dans l'eau ou une zone dangereuse — téléportation annulée.";
            _strings["teleport_dark_matter"]  = "Matière fantôme détectée — téléportation impossible.";
            _strings["teleport_need_suit"]    = "Zone dangereuse — enfilez votre combinaison avant de vous téléporter.";
            _strings["teleport_hazard_blocked"] = "Zone dangereuse — aucun point d'arrivée sûr trouvé.";

            // ===== SHIP LOG READER =====
            _strings["logreader_open_summary"]  = "Journal de bord. {0} planètes, {1} entrées au total, {2} explorées, {3} rumeurs.";
            _strings["logreader_closed"]       = "Journal fermé.";
            _strings["logreader_planet"]       = "{0}, {1} entrées, {2} explorées, {3} rumeurs";
            _strings["logreader_entry"]        = "{0}, {1}, {2} faits";
            _strings["logreader_no_entries"]   = "Aucune découverte dans le journal.";
            _strings["logreader_no_facts"]     = "Aucun fait disponible.";
            _strings["logreader_back_planets"] = "Retour aux planètes.";
            _strings["logreader_back_entries"] = "Retour aux entrées de {0}.";
            _strings["logreader_unavailable"]  = "Journal de bord indisponible.";

            // ===== GHOST MATTER HANDLER =====
            _strings["ghost_matter_near"]  = "Attention — matière fantôme à proximité !";
            _strings["ghost_matter_clear"] = "Zone dégagée.";

            // ===== QUANTUM HANDLER =====
            _strings["quantum_object_moved"] = "Un objet quantique s'est déplacé près de vous.";

            // ===== DARK BRAMBLE HANDLER =====
            _strings["angler_spotted"]       = "Anglerfish repéré, {0}, à {1} mètres.";
            _strings["angler_investigating"] = "Un anglerfish enquête.";
            _strings["angler_chasing"]       = "Un anglerfish vous charge !";
            _strings["angler_lost"]          = "L'anglerfish vous a perdu.";

            // ===== ELEVATOR HANDLER =====
            _strings["elevator_going_up"]   = "Ascenseur en montée.";
            _strings["elevator_going_down"] = "Ascenseur en descente.";
            _strings["elevator_arrived"]    = "Ascenseur arrivé.";

            // ===== GRAVITY HANDLER =====
            _strings["gravity_zero"]     = "Gravité nulle. Utilisez le jetpack.";
            _strings["gravity_restored"] = "Gravité rétablie.";
            _strings["gravity_flipped"]  = "La gravité a basculé.";

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
            _strings["pilot_status_on_body"]       = "Posé sur {0}.";
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

            // ===== SCOUT HANDLER =====
            _strings["scout_launched"]          = "Guetteur lancé.";
            _strings["scout_anchored"]          = "Guetteur ancré.";
            _strings["scout_retrieved"]         = "Guetteur récupéré.";
            _strings["scout_destroyed"]         = "Guetteur détruit !";
            _strings["scout_snapshot"]          = "Photo prise.";
            _strings["scout_interference_on"]   = "Interférence détectée sur le guetteur.";
            _strings["scout_interference_off"]  = "Interférence dissipée.";
            _strings["scout_available"]         = "Guetteur disponible.";
            _strings["scout_unavailable"]       = "Guetteur indisponible.";
            _strings["scout_distance"]          = "Guetteur : {0} mètres";
            _strings["scout_anchored_time_min"] = "ancré depuis {0} minutes {1} secondes";
            _strings["scout_anchored_time_sec"] = "ancré depuis {0} secondes";
            _strings["scout_retrieving"]        = "en cours de récupération";
            _strings["scout_in_flight"]         = "en vol";
            _strings["scout_has_interference"]  = "interférence";

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
            _strings["help_key_m"]          = "B";
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
            _strings["help_desc_m"]         = "Auto-walk to target";
            _strings["help_desc_t"]         = "Teleport to target — same planet, 500 meters max";
            _strings["help_desc_h"]         = "Personal status — health, oxygen, jetpack, boost, suit";
            _strings["help_desc_j"]         = "Ship status — fuel, oxygen, hull, damage";
            _strings["help_desc_k"]         = "Environment — active hazards, gravity, water";
            _strings["help_desc_i"]         = "Flight telemetry — speed, altitude, damage";
            _strings["help_desc_home_pilot"]       = "Select autopilot destination — at ship controls";
            _strings["help_desc_pageupdown_pilot"] = "Cycle planets — at ship controls";
            _strings["help_desc_end_pilot"]        = "Launch autopilot to destination — at ship controls";
            _strings["help_desc_u"]         = "Signal scope status — frequency and detected signal";
            _strings["help_key_o"]          = "O";
            _strings["help_desc_o"]         = "Scout probe status — distance, anchor, interference";

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
            _strings["nav_repair_item"]              = "{0} damaged, integrity {1}%";
            _strings["repair_focus"]                 = "{0} repairable, integrity {1}%.";
            _strings["repair_started"]               = "Repairing.";
            _strings["repair_progress"]              = "{0}%.";
            _strings["repair_finished"]              = "Repair complete.";
            _strings["repair_interrupted"]           = "Repair interrupted at {0}%.";
            _strings["repair_part_ShipPartTop"]      = "Top hull";
            _strings["repair_part_ShipPartLanding"]  = "Bottom hull";
            _strings["repair_part_ShipPartForward"]  = "Front hull";
            _strings["repair_part_ShipPartAft"]      = "Rear hull";
            _strings["repair_part_ShipPartPort"]     = "Port hull";
            _strings["repair_part_ShipPartStarboard"] = "Starboard hull";
            _strings["repair_part_ShipPartO2"]       = "Oxygen tank";
            _strings["repair_part_ShipPartFuel"]     = "Fuel tank";
            _strings["repair_part_ShipPartElectric"] = "Electrical system";
            _strings["repair_part_ShipPartReactor"]  = "Reactor";
            _strings["repair_part_ShipPartGravity"]  = "Gravity floor";
            _strings["repair_part_ShipPartAutopilot"] = "Autopilot";
            _strings["repair_part_ShipPartLights"]   = "Headlights";
            _strings["repair_part_ShipPartCamera"]   = "Rear camera";
            _strings["repair_part_ShipPartLeftThrust"]  = "Left thruster";
            _strings["repair_part_ShipPartRightThrust"] = "Right thruster";
            _strings["repair_part_ShipPartUnknown"]  = "Unknown part";
            _strings["nav_model_rocket"]  = "Model rocket";
            _strings["nav_nomai_statue"]  = "Nomai statue";
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
            _strings["loc_sun"]                = "Sun";
            _strings["loc_ash_twin"]           = "Ash Twin";
            _strings["loc_ember_twin"]         = "Ember Twin";
            _strings["loc_hourglass_twins"]    = "Hourglass Twins";
            _strings["loc_timber_hearth"]      = "Timber Hearth";
            _strings["loc_brittle_hollow"]     = "Brittle Hollow";
            _strings["loc_giants_deep"]        = "Giant's Deep";
            _strings["loc_dark_bramble"]       = "Dark Bramble";
            _strings["loc_comet"]              = "The Interloper";
            _strings["loc_quantum_moon"]       = "Quantum Moon";
            _strings["loc_timber_moon"]        = "The Attlerock";
            _strings["loc_volcanic_moon"]      = "Hollow's Lantern";
            _strings["loc_bramble_dimension"]  = "Bramble Dimension";
            _strings["loc_probe_cannon"]       = "Orbital Probe Cannon";
            _strings["loc_eye"]                = "Eye of the Universe";
            _strings["loc_sun_station"]        = "Sun Station";
            _strings["loc_white_hole"]         = "White Hole";
            _strings["loc_time_loop_device"]   = "Time Loop Device";
            _strings["loc_vessel"]             = "Nomai Vessel";
            _strings["loc_vessel_dimension"]   = "Nomai Vessel Dimension";
            _strings["loc_dream_world"]        = "Dream World";
            _strings["loc_invisible_planet"]   = "The Stranger";

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
            _strings["auto_walk_unsafe_path"]   = "Unsafe path ahead, auto-walk cancelled.";
            _strings["fluid_water"]             = "Water";
            _strings["fluid_sand"]              = "Falling sand";
            _strings["fluid_plasma"]            = "Solar plasma";
            _strings["fluid_geyser"]            = "Geyser";
            _strings["fluid_tractor"]           = "Tractor beam";
            _strings["auto_walk_wading"]        = "Shallow water — still walking.";
            _strings["fluid_deep_water"]        = "Deep water";
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
            _strings["autopilot_aligned"]       = "Ship aligned with surface.";
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
            _strings["autopilot_planet_item_km"]     = "{0} of {1}: {2}, {3} kilometers.";
            _strings["autopilot_planet_item_no_dist"] = "{0} of {1}: {2}.";

            // ===== MODEL ROCKET =====
            _strings["model_rocket_console_enter"]  = "Model rocket console. End to autopilot to geyser.";
            _strings["model_rocket_autopilot_on"]    = "Rocket autopilot engaged. Flying to geyser.";
            _strings["model_rocket_autopilot_off"]   = "Rocket autopilot disengaged.";
            _strings["model_rocket_no_target"]       = "No geyser found.";
            _strings["model_rocket_landed"]          = "Rocket landed on the geyser!";
            _strings["model_rocket_distance"]        = "{0} metres.";

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
            _strings["action_inside_ship"]   = "Action not available inside the ship.";
            _strings["teleport_success"]     = "Teleported to {0}.";
            _strings["teleport_unsafe"]       = "Unsafe landing zone — teleportation cancelled.";
            _strings["teleport_unsafe_water"] = "{0} is in water or a hazardous zone — teleportation cancelled.";
            _strings["teleport_dark_matter"]  = "Ghost matter detected — teleportation impossible.";
            _strings["teleport_need_suit"]    = "Hazardous zone — put on your suit before teleporting.";
            _strings["teleport_hazard_blocked"] = "Hazardous zone — no safe landing point found.";

            // ===== SHIP LOG READER =====
            _strings["logreader_open_summary"]  = "Ship log. {0} planets, {1} entries total, {2} explored, {3} rumored.";
            _strings["logreader_closed"]       = "Log closed.";
            _strings["logreader_planet"]       = "{0}, {1} entries, {2} explored, {3} rumored";
            _strings["logreader_entry"]        = "{0}, {1}, {2} facts";
            _strings["logreader_no_entries"]   = "No discoveries in the log.";
            _strings["logreader_no_facts"]     = "No facts available.";
            _strings["logreader_back_planets"] = "Back to planets.";
            _strings["logreader_back_entries"] = "Back to entries for {0}.";
            _strings["logreader_unavailable"]  = "Ship log unavailable.";

            // ===== GHOST MATTER HANDLER =====
            _strings["ghost_matter_near"]  = "Warning — ghost matter nearby!";
            _strings["ghost_matter_clear"] = "Area clear.";

            // ===== QUANTUM HANDLER =====
            _strings["quantum_object_moved"] = "A quantum object moved near you.";

            // ===== DARK BRAMBLE HANDLER =====
            _strings["angler_spotted"]       = "Anglerfish spotted, {0}, {1} meters.";
            _strings["angler_investigating"] = "An anglerfish is investigating.";
            _strings["angler_chasing"]       = "An anglerfish is charging at you!";
            _strings["angler_lost"]          = "The anglerfish lost you.";

            // ===== ELEVATOR HANDLER =====
            _strings["elevator_going_up"]   = "Elevator going up.";
            _strings["elevator_going_down"] = "Elevator going down.";
            _strings["elevator_arrived"]    = "Elevator arrived.";

            // ===== GRAVITY HANDLER =====
            _strings["gravity_zero"]     = "Zero gravity. Use the jetpack.";
            _strings["gravity_restored"] = "Gravity restored.";
            _strings["gravity_flipped"]  = "Gravity has flipped.";

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
            _strings["pilot_status_on_body"]       = "Landed on {0}.";
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

            // ===== SCOUT HANDLER =====
            _strings["scout_launched"]          = "Scout launched.";
            _strings["scout_anchored"]          = "Scout anchored.";
            _strings["scout_retrieved"]         = "Scout retrieved.";
            _strings["scout_destroyed"]         = "Scout destroyed!";
            _strings["scout_snapshot"]          = "Snapshot taken.";
            _strings["scout_interference_on"]   = "Scout interference detected.";
            _strings["scout_interference_off"]  = "Interference cleared.";
            _strings["scout_available"]         = "Scout available.";
            _strings["scout_unavailable"]       = "Scout unavailable.";
            _strings["scout_distance"]          = "Scout: {0} meters";
            _strings["scout_anchored_time_min"] = "anchored for {0} minutes {1} seconds";
            _strings["scout_anchored_time_sec"] = "anchored for {0} seconds";
            _strings["scout_retrieving"]        = "being retrieved";
            _strings["scout_in_flight"]         = "in flight";
            _strings["scout_has_interference"]  = "interference";

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

        private static void InitializeKeyLabelsGerman()
        {
            _keyLabels["CONFIRM"]    = "Bestätigen";
            _keyLabels["CANCEL"]     = "Abbrechen";
            _keyLabels["INTERACT"]   = "Interagieren";
            _keyLabels["JUMP"]       = "Springen";
            _keyLabels["ENTER"]      = "Eingabe";
            _keyLabels["BACK"]       = "Zurück";
            _keyLabels["PAUSE"]      = "Pause";
            _keyLabels["MAP"]        = "Karte";
            _keyLabels["SUIT"]       = "Anzug";
            _keyLabels["FLASHLIGHT"] = "Taschenlampe";
        }

        private static void InitializeGerman()
        {
            // ===== GENERAL =====
            _strings["mod_loaded"]  = "Outer Wilds Access geladen. Drücke F1 für Hilfe.";
            _strings["debug_on"]    = "Debug-Modus aktiviert.";
            _strings["debug_off"]   = "Debug-Modus deaktiviert.";
            _strings["mod_disabled"] = "Outer Wilds Access deaktiviert.";
            _strings["mod_enabled"]  = "Outer Wilds Access aktiviert.";

            // ===== HELP MENU (F1) =====
            _strings["help_open"]        = "Hilfe geöffnet. Pfeiltasten zum Navigieren. Eingabe, um eine Kategorie zu öffnen. Rücktaste, um zurückzugehen. Escape zum Schließen.";
            _strings["help_close"]       = "Hilfe geschlossen.";
            _strings["help_category"]    = "{0}, {1} Tastenkürzel.";
            _strings["help_cat_entered"] = "{0}, {1} Tastenkürzel.";
            _strings["help_item"]        = "{0}: {1}";

            // Category names
            _strings["help_cat_general"]    = "Allgemein";
            _strings["help_cat_navigation"] = "Navigation";
            _strings["help_cat_status"]     = "Status";
            _strings["help_cat_ship"]       = "Schiff";
            _strings["help_cat_tools"]      = "Werkzeuge";
            _strings["help_cat_settings"]   = "Einstellungen";

            // Key names
            _strings["help_key_f1"]         = "F1";
            _strings["help_key_f2"]         = "F2";
            _strings["help_key_f3"]         = "F3";
            _strings["help_key_f4"]         = "F4";
            _strings["help_key_f5"]         = "F5";
            _strings["help_key_f6"]         = "F6";
            _strings["help_key_f12"]        = "F12";
            _strings["help_key_delete"]     = "Entf";
            _strings["help_key_backspace"]  = "Rücktaste";
            _strings["help_key_home"]       = "Pos1";
            _strings["help_key_end"]        = "Ende";
            _strings["help_key_pageupdown"] = "Bild auf und Bild ab";
            _strings["help_key_altpage"]    = "Alt + Bild auf und Bild ab";
            _strings["help_key_g"]          = "G";
            _strings["help_key_h"]          = "H";
            _strings["help_key_i"]          = "I";
            _strings["help_key_j"]          = "J";
            _strings["help_key_k"]          = "K";
            _strings["help_key_l"]          = "L";
            _strings["help_key_m"]          = "B";
            _strings["help_key_t"]          = "T";
            _strings["help_key_u"]          = "U";

            // Descriptions
            _strings["help_desc_f1"]        = "Hilfe — öffnet dieses Menü";
            _strings["help_desc_f2"]        = "Verbleibende Zeit bis zur Supernova";
            _strings["help_desc_f3"]        = "Schiff über dir herbeirufen";
            _strings["help_desc_f4"]        = "Schiffslogbuch — durchsuche deine Entdeckungen";
            _strings["help_desc_f5"]        = "Mod deaktivieren oder wieder aktivieren";
            _strings["help_desc_f6"]        = "Einstellungsmenü öffnen";
            _strings["help_desc_f12"]       = "Debug-Modus umschalten";
            _strings["help_desc_delete"]    = "Letzte Ansage wiederholen";
            _strings["help_desc_backspace"] = "Audio-Signalton stummschalten oder wieder einschalten";
            _strings["help_desc_home_nav"]  = "Objekte in der Nähe scannen";
            _strings["help_desc_pageupdown_nav"]  = "Durch gescannte Objekte blättern";
            _strings["help_desc_altpage"]   = "Navigationskategorie wechseln";
            _strings["help_desc_end_nav"]   = "Entfernung und Richtung zum Ziel";
            _strings["help_desc_l"]         = "Detaillierte Position — Planet, Zone und Ort in der Nähe";
            _strings["help_desc_g"]         = "Audio-Wegführung zum Ziel mit Tonsignalen";
            _strings["help_desc_m"]         = "Automatisches Gehen zum Ziel";
            _strings["help_desc_t"]         = "Teleportieren zum Ziel — selber Planet, maximal 500 Meter";
            _strings["help_desc_h"]         = "Persönlicher Status — Gesundheit, Sauerstoff, Jetpack, Boost, Anzug";
            _strings["help_desc_j"]         = "Schiffsstatus — Treibstoff, Sauerstoff, Hülle, Schäden";
            _strings["help_desc_k"]         = "Umgebung — aktive Gefahren, Schwerkraft, Wasser";
            _strings["help_desc_i"]         = "Flugtelemetrie — Geschwindigkeit, Höhe, Schäden";
            _strings["help_desc_home_pilot"]       = "Autopilot-Ziel auswählen — am Steuerpult";
            _strings["help_desc_pageupdown_pilot"] = "Planeten durchblättern — am Steuerpult";
            _strings["help_desc_end_pilot"]        = "Autopilot zum Ziel starten — am Steuerpult";
            _strings["help_desc_u"]         = "Signalrohr-Status — Frequenz und erkanntes Signal";
            _strings["help_key_o"]          = "O";
            _strings["help_desc_o"]         = "Sondenstatus — Entfernung, Verankerung, Störung";

            // ===== MENU HANDLER =====
            _strings["toggle_on"]       = "Aktiviert";
            _strings["toggle_off"]      = "Deaktiviert";
            _strings["slider_value"]    = "{0} von 10";
            _strings["rebinding_enter"] = "Drücke eine Taste zum Zuweisen.";
            _strings["rebinding_done"]  = "Taste zugewiesen: {0}.";
            _strings["rebinding_cancel"]= "Abgebrochen.";

            // ===== STATE HANDLER =====
            // Death causes
            _strings["death_default"]          = "Tot.";
            _strings["death_impact"]           = "Beim Aufprall gestorben.";
            _strings["death_asphyxiation"]     = "Erstickt — kein Sauerstoff mehr.";
            _strings["death_energy"]           = "Durch Stromschlag getötet.";
            _strings["death_supernova"]        = "Von der Supernova verschlungen.";
            _strings["death_digestion"]        = "Von der Pflanze verdaut.";
            _strings["death_bigbang"]          = "Ende der Schleife — die Sonne explodiert.";
            _strings["death_crushed"]          = "Zerquetscht.";
            _strings["death_meditation"]       = "Meditation — überspringe zum nächsten Zyklus.";
            _strings["death_timeloop"]         = "Ende der Zeitschleife.";
            _strings["death_lava"]             = "Durch Lava getötet.";
            _strings["death_blackhole"]        = "In ein schwarzes Loch gesogen.";
            _strings["death_dream"]            = "Im Traum gestorben.";
            _strings["death_dreamexplosion"]   = "Explosion im Traum.";
            _strings["death_crushedbyelevator"] = "Vom Aufzug zerquetscht.";

            // Respawn / cycle
            _strings["player_respawn"]         = "Neuer Zyklus. Du bist zurück am Schiff.";

            // Ship
            _strings["enter_ship"]             = "Im Schiff.";
            _strings["exit_ship"]              = "Schiff verlassen.";
            _strings["enter_flight_console"]   = "Am Steuerpult.";
            _strings["exit_flight_console"]    = "Steuerpult verlassen.";
            _strings["ship_hull_breach"]       = "Warnung — Schiffshülle durchbrochen!";
            _strings["enter_ship_computer"]    = "Schiffslogbuch geöffnet.";
            _strings["exit_ship_computer"]     = "Schiffslogbuch geschlossen.";
            _strings["enter_landing_view"]     = "Landekamera aktiviert.";
            _strings["exit_landing_view"]      = "Landekamera deaktiviert.";

            // Equipment
            _strings["suit_on"]                = "Anzug angelegt.";
            _strings["suit_off"]               = "Anzug abgelegt.";
            _strings["flashlight_on"]          = "Taschenlampe an.";
            _strings["flashlight_off"]         = "Taschenlampe aus.";
            _strings["equip_signalscope"]      = "Signalrohr ausgerüstet.";
            _strings["unequip_signalscope"]    = "Signalrohr verstaut.";
            _strings["equip_translator"]       = "Übersetzer ausgerüstet.";
            _strings["unequip_translator"]     = "Übersetzer verstaut.";

            // Map
            _strings["enter_map"]              = "Sonnensystemkarte geöffnet.";
            _strings["exit_map"]               = "Karte geschlossen.";

            // Time
            _strings["fast_forward_start"]     = "Zeit wird vorgespult.";
            _strings["fast_forward_end"]       = "Normale Zeit.";

            // Conversation
            _strings["enter_conversation"]     = "Dialog.";
            _strings["exit_conversation"]      = "Dialog beendet.";

            // Signalscope
            _strings["enter_signalscope"]      = "Signalrohr aktiviert.";
            _strings["exit_signalscope"]       = "Signalrohr deaktiviert.";

            // ===== NAVIGATION HANDLER =====
            _strings["nav_ship"]          = "Schiff";
            _strings["nav_repair_item"]              = "{0} beschädigt, Integrität {1} %";
            _strings["repair_focus"]                 = "{0} reparierbar, Integrität {1} %.";
            _strings["repair_started"]               = "Reparatur läuft.";
            _strings["repair_progress"]              = "{0} %.";
            _strings["repair_finished"]              = "Reparatur abgeschlossen.";
            _strings["repair_interrupted"]           = "Reparatur bei {0} % unterbrochen.";
            _strings["repair_part_ShipPartTop"]      = "Obere Hülle";
            _strings["repair_part_ShipPartLanding"]  = "Untere Hülle";
            _strings["repair_part_ShipPartForward"]  = "Bughülle";
            _strings["repair_part_ShipPartAft"]      = "Heckhülle";
            _strings["repair_part_ShipPartPort"]     = "Backbordhülle";
            _strings["repair_part_ShipPartStarboard"] = "Steuerbordhülle";
            _strings["repair_part_ShipPartO2"]       = "Sauerstofftank";
            _strings["repair_part_ShipPartFuel"]     = "Treibstofftank";
            _strings["repair_part_ShipPartElectric"] = "Elektrik";
            _strings["repair_part_ShipPartReactor"]  = "Reaktor";
            _strings["repair_part_ShipPartGravity"]  = "Gravitationsboden";
            _strings["repair_part_ShipPartAutopilot"] = "Autopilot";
            _strings["repair_part_ShipPartLights"]   = "Scheinwerfer";
            _strings["repair_part_ShipPartCamera"]   = "Heckkamera";
            _strings["repair_part_ShipPartLeftThrust"]  = "Linkes Triebwerk";
            _strings["repair_part_ShipPartRightThrust"] = "Rechtes Triebwerk";
            _strings["repair_part_ShipPartUnknown"]  = "Unbekanntes Teil";
            _strings["nav_model_rocket"]  = "Modellrakete";
            _strings["nav_nomai_statue"]  = "Nomai-Statue";
            _strings["nav_nothing_found"] = "Keine Objekte in der Nähe.";
            _strings["nav_scan_first"]    = "{0} Objekt(e) gefunden. {1}, {2} Meter.";
            _strings["nav_item"]          = "{0} von {1}: {2}, {3} Meter.";
            _strings["nav_stale"]         = "Liste veraltet — drücke Pos1 zum Aktualisieren.";
            _strings["nav_no_scan"]       = "Scanne zuerst mit der Pos1-Taste.";
            _strings["nav_no_target"]     = "Wähle ein Objekt mit Bild ab und drücke dann Ende.";
            _strings["nav_target_lost"]    = "Ziel verloren.";
            _strings["nav_target_cleared"] = "Ziel gelöscht.";
            _strings["nav_navigate"]      = "{0}: {1}, {2}m";
            _strings["nav_interact_hint"] = "Drücke {0} zum Interagieren.";
            _strings["nav_north"]         = "vorn";
            _strings["nav_south"]         = "hinten";
            _strings["nav_east"]          = "rechts";
            _strings["nav_west"]          = "links";
            _strings["nav_up"]            = "oben";
            _strings["nav_down"]          = "unten";
            _strings["nav_here"]          = "hier";
            _strings["nav_live"]          = "{0}, {1}m";

            // ===== CATEGORY NAVIGATION (Alt+PageUp/Down) =====
            _strings["nav_cat_ship"]           = "Schiff";
            _strings["nav_cat_npcs"]           = "Charaktere";
            _strings["nav_cat_interactables"]  = "Interagierbares";
            _strings["nav_cat_nomai"]          = "Nomai-Texte";
            _strings["nav_cat_locations"]      = "Orte";
            _strings["nav_cat_signs"]          = "Schilder";
            _strings["nav_cat_announce"]       = "{0}: {1} Ergebnis(se). {2}, {3} Meter.";
            _strings["nav_cat_empty"]          = "{0}: keine Ergebnisse.";

            // Nomai text labels (object scan)
            _strings["nav_nomai_wall"]       = "Nomai-Wandtext";
            _strings["nav_nomai_computer"]   = "Nomai-Computer";

            // Campfire labels
            _strings["nav_campfire"]            = "Lagerfeuer";
            _strings["nav_campfire_lit"]         = "brennend";
            _strings["nav_campfire_smoldering"]  = "schwelend";
            _strings["nav_campfire_unlit"]       = "erloschen";

            // Sub-sector translations
            _strings["sector_village"]             = "Dorf";
            _strings["sector_zerogcave"]           = "Schwerelosigkeitshöhle";
            _strings["sector_observatory"]         = "Observatorium";
            _strings["sector_museum"]              = "Museum";
            _strings["sector_north_pole"]          = "Nordpol";
            _strings["sector_south_pole"]          = "Südpol";
            _strings["sector_crossroads"]          = "Kreuzung";
            _strings["sector_canyons"]             = "Schluchten";
            _strings["sector_anglerfish"]          = "Anglerfisch";
            _strings["sector_oldsettle"]           = "Alte Siedlung";
            _strings["sector_gravitycannon"]       = "Schwerkraftkanone";
            _strings["sector_towerofknowledge"]    = "Turm des Wissens";
            _strings["sector_blackholeforge"]      = "Schwarzlochschmiede";
            _strings["sector_hangingcity"]         = "Hängende Stadt";
            _strings["sector_constructionyard"]    = "Bauwerft";
            _strings["sector_escape_pod"]          = "Rettungskapsel";
            _strings["sector_thlanding"]           = "Landezone";
            _strings["sector_geyser"]              = "Geysir";
            _strings["sector_undergroundlake"]     = "Unterirdischer See";
            _strings["sector_quantumgrove"]        = "Quantenhain";
            _strings["sector_quantumcaves"]        = "Quantenhöhlen";

            // ===== LOCATION HANDLER =====
            _strings["location_enter"]         = "Angekommen: {0}.";
            _strings["location_current"]       = "Position: {0}.";
            _strings["location_space"]         = "Im Orbit im Weltraum.";
            _strings["location_unknown"]       = "Position unbekannt.";
            _strings["location_near"]          = "in der Nähe von {0}";

            // Planet / zone names (official German names)
            _strings["loc_sun"]                = "Sonne";
            _strings["loc_ash_twin"]           = "Aschzwilling";
            _strings["loc_ember_twin"]         = "Glühzwilling";
            _strings["loc_hourglass_twins"]    = "Sanduhrzwillinge";
            _strings["loc_timber_hearth"]      = "Holzheim";
            _strings["loc_brittle_hollow"]     = "Bruchhöhle";
            _strings["loc_giants_deep"]        = "Tiefe des Riesen";
            _strings["loc_dark_bramble"]       = "Dunkles Dornengestrüpp";
            _strings["loc_comet"]              = "Der Eindringling";
            _strings["loc_quantum_moon"]       = "Quantenmond";
            _strings["loc_timber_moon"]        = "Der Attlerock";
            _strings["loc_volcanic_moon"]      = "Bruchhöhlenlaterne";
            _strings["loc_bramble_dimension"]  = "Dornengestrüpp-Dimension";
            _strings["loc_probe_cannon"]       = "Orbitale Sondenkanone";
            _strings["loc_eye"]                = "Auge des Universums";
            _strings["loc_sun_station"]        = "Sonnenstation";
            _strings["loc_white_hole"]         = "Weißes Loch";
            _strings["loc_time_loop_device"]   = "Zeitschleifen-Vorrichtung";
            _strings["loc_vessel"]             = "Nomai-Schiff";
            _strings["loc_vessel_dimension"]   = "Nomai-Schiff-Dimension";
            _strings["loc_dream_world"]        = "Traumwelt";
            _strings["loc_invisible_planet"]   = "Der Fremdling";

            // Environment
            _strings["camera_enter_water"]     = "Kamera unter Wasser.";
            _strings["attach_to_point"]        = "An einer Oberfläche befestigt.";
            _strings["detach_from_point"]      = "Von Oberfläche gelöst.";
            _strings["enter_undertow"]         = "Von Sog erfasst.";
            _strings["exit_undertow"]          = "Sog verlassen.";
            _strings["enter_dark_zone"]        = "Dunkle Zone — Licht funktioniert hier nicht.";
            _strings["exit_dark_zone"]         = "Dunkle Zone verlassen.";
            _strings["enter_dream_world"]      = "Traumwelt betreten.";
            _strings["exit_dream_world"]       = "Traumwelt verlassen.";
            _strings["player_grabbed_ghost"]   = "Von einem Geist gepackt!";
            _strings["player_released_ghost"]  = "Vom Geist losgelassen.";

            // ===== AUTO-WALK HANDLER =====
            _strings["auto_walk_hazard"]        = "Gefahr — {0}! Automatisches Gehen gestoppt.";
            _strings["hazard_fire"]             = "Feuer";
            _strings["hazard_heat"]             = "Extreme Hitze";
            _strings["hazard_darkmatter"]       = "Dunkle Materie";
            _strings["hazard_electricity"]      = "Elektrizität";
            _strings["hazard_sandfall"]         = "Sandfall";
            _strings["hazard_generic"]          = "Gefahrenzone";
            _strings["auto_walk_stuck"]         = "Weg blockiert. Automatisches Gehen gestoppt.";
            _strings["auto_walk_unsafe_path"]   = "Unsicherer Weg, automatisches Gehen abgebrochen.";
            _strings["fluid_water"]             = "Wasser";
            _strings["fluid_sand"]              = "Fallender Sand";
            _strings["fluid_plasma"]            = "Sonnenplasma";
            _strings["fluid_geyser"]            = "Geysir";
            _strings["fluid_tractor"]           = "Traktorstrahl";
            _strings["auto_walk_wading"]        = "Seichtes Wasser — gehe weiter.";
            _strings["fluid_deep_water"]        = "Tiefes Wasser";
            _strings["auto_walk_out_of_reach"]  = "{0} ist außer Reichweite — zu hoch oder zu niedrig.";
            _strings["auto_walk_on"]            = "Automatisches Gehen zu {0}.";
            _strings["auto_walk_off"]           = "Automatisches Gehen gestoppt.";
            _strings["auto_walk_arrived"]       = "Bei {0} angekommen.";
            _strings["auto_walk_cliff"]         = "Klippe — Umleitung.";
            _strings["auto_walk_steering"]      = "Hindernis — Umleitung.";
            _strings["auto_walk_jump"]          = "Sprung.";

            // ===== PATH GUIDANCE HANDLER =====
            _strings["guidance_on"]       = "Führung zu {0}.";
            _strings["guidance_off"]      = "Führung gestoppt.";
            _strings["guidance_arrived"]  = "Bei {0} angekommen.";
            _strings["auto_walk_no_path"]       = "Kein Weg gefunden. Automatisches Gehen gestoppt.";
            _strings["auto_walk_danger_steer"]  = "Gefahr erkannt — Umleitung.";

            // ===== SHIP LOG HANDLER =====
            _strings["shiplog_updated"]        = "Logbuch aktualisiert.";
            _strings["shiplog_explored"]       = "Erkundet";
            _strings["shiplog_rumored"]        = "Gerücht";
            _strings["shiplog_no_discoveries"] = "Keine Entdeckungen.";
            _strings["shiplog_back_to_map"]    = "Zurück zur Karte.";
            _strings["shiplog_detective_reveal"] = "Neue Entdeckungen: {0}. Drücke E zum Fortfahren, dann Q für den Kartenmodus.";

            // ===== AUTOPILOT =====
            _strings["autopilot_select"]        = "Zielauswahl. Bild auf oder ab zum Wählen. Ende zum Bestätigen.";
            _strings["autopilot_no_console"]    = "Du musst am Steuerpult sein.";
            _strings["autopilot_initiated"]     = "Autopilot zu {0}.";
            _strings["autopilot_arrived"]       = "Bei {0} angekommen.";
            _strings["autopilot_aligned"]       = "Schiff mit Oberfläche ausgerichtet.";
            _strings["autopilot_retro"]         = "Bremsen.";
            _strings["autopilot_aborted"]       = "Autopilot abgebrochen.";
            _strings["autopilot_cancelled"]     = "Auswahl abgebrochen.";
            _strings["autopilot_already_close"] = "Bereits in der Nähe von {0}.";
            _strings["autopilot_failed"]        = "Autopilot nicht verfügbar.";
            _strings["autopilot_damaged"]       = "Autopilot beschädigt.";
            _strings["autopilot_aligning"]           = "Ausrichten zum Ziel.";
            _strings["autopilot_accelerating"]       = "Beschleunige zum Ziel.";
            _strings["autopilot_matching_velocity"]  = "Geschwindigkeit angleichen.";
            _strings["autopilot_velocity_matched"]   = "Geschwindigkeit angeglichen.";
            _strings["autopilot_planet_item"]        = "{0} von {1}: {2}, {3} Meter.";
            _strings["autopilot_planet_item_km"]     = "{0} von {1}: {2}, {3} Kilometer.";
            _strings["autopilot_planet_item_no_dist"] = "{0} von {1}: {2}.";

            // ===== MODEL ROCKET =====
            _strings["model_rocket_console_enter"]  = "Modellraketen-Konsole. Ende für Autopilot zum Geysir.";
            _strings["model_rocket_autopilot_on"]    = "Raketen-Autopilot aktiviert. Flug zum Geysir.";
            _strings["model_rocket_autopilot_off"]   = "Raketen-Autopilot deaktiviert.";
            _strings["model_rocket_no_target"]       = "Kein Geysir gefunden.";
            _strings["model_rocket_landed"]          = "Rakete auf dem Geysir gelandet!";
            _strings["model_rocket_distance"]        = "{0} Meter.";

            // ===== SHIP RECALL =====
            _strings["recall_success"]     = "Schiff herbeigerufen.";
            _strings["recall_inside"]      = "Du bist bereits im Schiff.";
            _strings["recall_destroyed"]   = "Schiff ist zerstört, Herbeirufen nicht möglich.";
            _strings["recall_unavailable"] = "Schiff-Herbeirufen nicht verfügbar.";

            // ===== LOOP TIMER =====
            _strings["timer_remaining"]    = "{0} Minuten und {1} Sekunden verbleibend.";
            _strings["timer_expired"]      = "Zeit abgelaufen.";
            _strings["timer_unavailable"]  = "Timer nicht verfügbar.";

            // Teleport
            _strings["teleport_no_target"]   = "Kein Ziel ausgewählt. Scanne zuerst mit Pos1, dann wähle mit Bild auf oder ab.";
            _strings["teleport_too_far"]     = "Ziel zu weit entfernt für Teleportation.";
            _strings["teleport_not_on_foot"] = "Teleportation nur zu Fuß verfügbar.";
            _strings["action_inside_ship"]   = "Aktion im Schiff nicht verfügbar.";
            _strings["teleport_success"]     = "Teleportiert zu {0}.";
            _strings["teleport_unsafe"]       = "Unsichere Landezone — Teleportation abgebrochen.";
            _strings["teleport_unsafe_water"] = "{0} liegt im Wasser oder in einer Gefahrenzone — Teleportation abgebrochen.";
            _strings["teleport_dark_matter"]  = "Geistermaterie erkannt — Teleportation unmöglich.";
            _strings["teleport_need_suit"]    = "Gefahrenzone — lege deinen Anzug an, bevor du dich teleportierst.";
            _strings["teleport_hazard_blocked"] = "Gefahrenzone — kein sicherer Landepunkt gefunden.";

            // ===== SHIP LOG READER =====
            _strings["logreader_open_summary"]  = "Schiffslogbuch. {0} Planeten, {1} Einträge insgesamt, {2} erkundet, {3} Gerüchte.";
            _strings["logreader_closed"]       = "Logbuch geschlossen.";
            _strings["logreader_planet"]       = "{0}, {1} Einträge, {2} erkundet, {3} Gerüchte";
            _strings["logreader_entry"]        = "{0}, {1}, {2} Fakten";
            _strings["logreader_no_entries"]   = "Keine Entdeckungen im Logbuch.";
            _strings["logreader_no_facts"]     = "Keine Fakten verfügbar.";
            _strings["logreader_back_planets"] = "Zurück zu den Planeten.";
            _strings["logreader_back_entries"] = "Zurück zu den Einträgen für {0}.";
            _strings["logreader_unavailable"]  = "Schiffslogbuch nicht verfügbar.";

            // ===== GHOST MATTER HANDLER =====
            _strings["ghost_matter_near"]  = "Warnung — Geistermaterie in der Nähe!";
            _strings["ghost_matter_clear"] = "Bereich frei.";

            // ===== QUANTUM HANDLER =====
            _strings["quantum_object_moved"] = "Ein Quantenobjekt hat sich in deiner Nähe bewegt.";

            // ===== DARK BRAMBLE HANDLER =====
            _strings["angler_spotted"]       = "Anglerfisch gesichtet, {0}, {1} Meter.";
            _strings["angler_investigating"] = "Ein Anglerfisch untersucht die Umgebung.";
            _strings["angler_chasing"]       = "Ein Anglerfisch greift dich an!";
            _strings["angler_lost"]          = "Der Anglerfisch hat dich verloren.";

            // ===== ELEVATOR HANDLER =====
            _strings["elevator_going_up"]   = "Aufzug fährt nach oben.";
            _strings["elevator_going_down"] = "Aufzug fährt nach unten.";
            _strings["elevator_arrived"]    = "Aufzug angekommen.";

            // ===== GRAVITY HANDLER =====
            _strings["gravity_zero"]     = "Schwerelosigkeit. Benutze das Jetpack.";
            _strings["gravity_restored"] = "Schwerkraft wiederhergestellt.";
            _strings["gravity_flipped"]  = "Schwerkraft hat sich umgekehrt.";

            // ===== RESOURCE MONITOR =====
            _strings["gauge_health"]    = "Gesundheit bei {0} Prozent.";
            _strings["gauge_oxygen"]    = "Sauerstoff bei {0} Prozent.";
            _strings["gauge_jetpack"]   = "Jetpack-Treibstoff bei {0} Prozent.";
            _strings["gauge_ship_fuel"] = "Schiffstreibstoff bei {0} Prozent.";

            // ===== ON-DEMAND STATUS (H / J / K) =====
            _strings["status_unavailable"]      = "Status nicht verfügbar.";
            _strings["status_health"]           = "Gesundheit {0} Prozent";
            _strings["status_oxygen_min"]       = "Sauerstoff {0} Minuten {1} Sekunden";
            _strings["status_oxygen_sec"]       = "Sauerstoff {0} Sekunden";
            _strings["status_jetpack"]          = "Jetpack {0} Prozent";
            _strings["status_boost"]            = "Boost {0} Prozent";
            _strings["status_suit_punctured"]   = "Anzug durchlöchert";
            _strings["status_ship_unavailable"] = "Schiff nicht verfügbar.";
            _strings["status_ship_fuel"]        = "Schiffstreibstoff {0} Prozent";
            _strings["status_ship_oxygen"]      = "Schiffssauerstoff {0} Prozent";
            _strings["status_ship_integrity"]   = "Hüllenintegrität {0} Prozent";
            _strings["status_ship_hull_breach"] = "Hülle durchbrochen";
            _strings["status_ship_ok"]          = "Keine Schäden";
            _strings["status_ship_reactor"]     = "Reaktor kritisch";
            _strings["status_ship_electrical"]  = "Elektrischer Ausfall";
            _strings["status_hazard"]           = "Gefahr: {0}, {1} Schaden pro Sekunde";
            _strings["status_no_hazard"]        = "Keine Gefahren";
            _strings["status_zero_g"]           = "Schwerelosigkeit";
            _strings["status_underwater"]       = "Unter Wasser";
            _strings["hazard_ghost_matter"]     = "Geistermaterie";
            _strings["hazard_sand"]             = "Sand";
            _strings["hazard_unknown"]          = "Unbekannt";

            // ===== ACCESSIBILITY MENU — items =====
            _strings["menu_item_gauge"]         = "Ressourcenwarnungen";
            _strings["menu_item_guidance"]      = "Audio-Tonführung";
            _strings["menu_item_meditation"]    = "Meditation von Anfang an freigeschaltet";
            _strings["menu_item_ghostmatterprotection"] = "Schutz vor Geistermaterie";
            _strings["menu_item_shiprecall"]    = "Schiff herbeirufen";
            _strings["menu_item_autopilot"]     = "Planeten-Autopilot";
            _strings["menu_item_peacefulghosts"] = "Friedliche Geister (DLC)";
            _strings["menu_item_nvdadirect"]    = "NVDA Direct-Speech-API";

            // ===== SHIP PILOT HANDLER =====
            _strings["pilot_speed_stationary"]  = "Stillstehend";
            _strings["pilot_speed_slow"]        = "Langsam";
            _strings["pilot_speed_moderate"]    = "Mäßig";
            _strings["pilot_speed_fast"]        = "Schnell";
            _strings["pilot_speed_very_fast"]   = "Sehr schnell";
            _strings["pilot_speed"]             = "Geschwindigkeit: {0}, {1} m/s.";
            _strings["pilot_altitude"]          = "Höhe: {0} Meter.";
            _strings["pilot_approach_warning"]  = "Warnung — Annäherung mit {0} m/s.";
            _strings["pilot_approach_danger"]   = "Gefahr — schnelle Annäherung mit {0} m/s!";
            _strings["pilot_liftoff"]           = "Abheben.";
            _strings["pilot_approach_body"]     = "Annäherung an {0}.";
            _strings["pilot_lost_target"]       = "Ziel verloren.";
            _strings["pilot_hull_damaged"]      = "Hülle beschädigt: {0}.";
            _strings["pilot_component_damaged"] = "Komponente beschädigt: {0}.";
            _strings["pilot_altimeter_on"]      = "Höhenmesser an.";
            _strings["pilot_altimeter_off"]     = "Höhenmesser aus.";
            _strings["pilot_part_top"]          = "Oben";
            _strings["pilot_part_forward"]      = "Vorn";
            _strings["pilot_part_port"]         = "Backbord";
            _strings["pilot_part_landing"]      = "Landegestell";
            _strings["pilot_part_starboard"]    = "Steuerbord";
            _strings["pilot_part_aft"]          = "Heck";
            _strings["pilot_part_autopilot"]    = "Autopilot";
            _strings["pilot_part_fuel"]         = "Treibstofftank";
            _strings["pilot_part_gravity"]      = "Schwerkraftgenerator";
            _strings["pilot_part_lights"]       = "Beleuchtung";
            _strings["pilot_part_camera"]       = "Landekamera";
            _strings["pilot_part_left_thrust"]  = "Linkes Triebwerk";
            _strings["pilot_part_electric"]     = "Elektrisches System";
            _strings["pilot_part_o2"]           = "Sauerstoffreserve";
            _strings["pilot_part_reactor"]      = "Reaktor";
            _strings["pilot_part_right_thrust"] = "Rechtes Triebwerk";
            _strings["pilot_not_at_console"]       = "Du musst am Steuerpult sein.";
            _strings["pilot_unavailable"]          = "Flugdaten nicht verfügbar.";
            _strings["pilot_status_speed"]         = "Geschwindigkeit: {0} m/s, {1}.";
            _strings["pilot_status_near"]          = "In der Nähe von {0}.";
            _strings["pilot_status_on_body"]       = "Gelandet auf {0}.";
            _strings["pilot_status_altitude"]      = "Höhe: {0} Meter.";
            _strings["pilot_status_hull_breach"]   = "Hüllenbruch!";
            _strings["pilot_status_damaged"]       = "Hülle bei {0} Prozent.";
            _strings["pilot_status_no_damage"]     = "Keine Schäden.";
            _strings["pilot_status_reactor_critical"] = "Reaktor kritisch!";
            _strings["pilot_status_electrical_fail"]  = "Elektrischer Ausfall!";
            _strings["pilot_status_landed"]        = "Schiff gelandet.";

            // ===== SIGNALSCOPE HANDLER =====
            _strings["scope_equipped"]             = "Signalrohr: {0}.";
            _strings["scope_frequency"]            = "Frequenz: {0}.";
            _strings["scope_signal_detected"]      = "Signal erkannt: {0}, {1}.";
            _strings["scope_signal_detected_dist"] = "Signal erkannt: {0}, {1}, {2} Meter.";
            _strings["scope_signal_lost"]          = "Signal verloren.";
            _strings["scope_signal_identified"]    = "Signal identifiziert: {0}!";
            _strings["scope_strength"]             = "Stärke: {0}.";
            _strings["scope_strength_dist"]        = "Stärke: {0}, {1} Meter.";
            _strings["scope_unknown_signal"]       = "Unbekanntes Signal";
            _strings["scope_str_very_weak"]  = "Sehr schwach";
            _strings["scope_str_weak"]       = "Schwach";
            _strings["scope_str_moderate"]   = "Mäßig";
            _strings["scope_str_strong"]     = "Stark";
            _strings["scope_str_maximum"]    = "Maximal";
            _strings["scope_not_equipped"]     = "Signalrohr ist nicht ausgerüstet.";
            _strings["scope_status_no_signal"] = "Frequenz {0}. Kein Signal erkannt.";
            _strings["scope_status_full"]      = "Frequenz {0}. {1}, {2}, {3} Meter, {4} Grad.";
            _strings["scope_status_partial"]   = "Frequenz {0}. {1}, {2}, {3} Grad.";

            // ===== SCOUT HANDLER =====
            _strings["scout_launched"]          = "Sonde abgeschossen.";
            _strings["scout_anchored"]          = "Sonde verankert.";
            _strings["scout_retrieved"]         = "Sonde eingeholt.";
            _strings["scout_destroyed"]         = "Sonde zerstört!";
            _strings["scout_snapshot"]          = "Foto aufgenommen.";
            _strings["scout_interference_on"]   = "Sondenstörung erkannt.";
            _strings["scout_interference_off"]  = "Störung beseitigt.";
            _strings["scout_available"]         = "Sonde verfügbar.";
            _strings["scout_unavailable"]       = "Sonde nicht verfügbar.";
            _strings["scout_distance"]          = "Sonde: {0} Meter";
            _strings["scout_anchored_time_min"] = "verankert seit {0} Minuten {1} Sekunden";
            _strings["scout_anchored_time_sec"] = "verankert seit {0} Sekunden";
            _strings["scout_retrieving"]        = "wird eingeholt";
            _strings["scout_in_flight"]         = "im Flug";
            _strings["scout_has_interference"]  = "Störung";

            // ===== NOMAI TEXT HANDLER =====
            _strings["nomai_root"]  = "Nachricht:";
            _strings["nomai_reply"] = "Antwort:";
            _strings["nomai_page"]  = "Seite {0} von {1}.";

            _strings["menu_item_collision"]     = "Kollisions-Piepton";
            _strings["menu_item_autowalk"]      = "Automatisches Gehen";
            _strings["menu_item_proximity"]     = "Nähe-Ansagen";
            _strings["proximity_nearby"]        = "{0}.";

            // ===== BEACON HANDLER =====
            _strings["beacon_on"]      = "Signalton aktiviert.";
            _strings["beacon_off"]     = "Signalton deaktiviert.";
            _strings["beacon_lost"]    = "Signalton-Ziel verloren.";
            _strings["beacon_muted"]   = "Signalton stummgeschaltet.";
            _strings["beacon_unmuted"] = "Signalton fortgesetzt.";

            // ===== ACCESSIBILITY MENU =====
            _strings["menu_open"]   = "Einstellungen geöffnet. Pfeile oder Bildtasten zum Navigieren. Eingabe zum Umschalten. F6 zum Schließen.";
            _strings["menu_closed"] = "Einstellungen gespeichert.";
            _strings["menu_cancel"] = "Einstellungen abgebrochen.";
            _strings["cheats_unlocked"] = "Erweiterte Optionen freigeschaltet.";
            _strings["menu_item_beacon"]     = "Audio-Signalton";
            _strings["menu_item_navigation"] = "Navigation";
            _strings["menu_item_status"]    = "{0}: {1}";
            _strings["menu_controls_hint"] = "Navigieren: Pfeile. Bestätigen: {0}. Zurück: {1}.";

            // ===== BUTTON LABELS (InputHelper) =====
            _strings["btn_enter"]       = "Eingabe";
            _strings["btn_space"]       = "Leertaste";
            _strings["btn_escape"]      = "Escape";
            _strings["btn_backspace"]   = "Rücktaste";
            _strings["btn_delete"]      = "Entf";
            _strings["btn_up"]          = "Oben";
            _strings["btn_down"]        = "Unten";
            _strings["btn_left"]        = "Links";
            _strings["btn_right"]       = "Rechts";
            _strings["btn_xbox_view"]   = "View";
            _strings["btn_ps_cross"]    = "Kreuz";
            _strings["btn_ps_circle"]   = "Kreis";
            _strings["btn_ps_square"]   = "Viereck";
            _strings["btn_ps_share"]    = "Share";
            _strings["btn_ps_create"]   = "Create";
            _strings["btn_ps_touchpad"] = "Touchpad";
            _strings["btn_dpad_up"]     = "Steuerkreuz oben";
            _strings["btn_dpad_down"]   = "Steuerkreuz unten";
            _strings["btn_dpad_left"]   = "Steuerkreuz links";
            _strings["btn_dpad_right"]  = "Steuerkreuz rechts";

            // ===== PROMPT FORMATTING =====
            _strings["prompt_and"]             = " und ";
            _strings["prompt_button_single"]   = "Taste";
            _strings["prompt_button_plural"]   = "Tasten";

            // ===== MISC =====
            _strings["backend_speech"]         = "Sprachausgabe-Backend: {0}";
            _strings["autowalk_patch_failed"]  = "Automatisches Gehen: Patch nicht angewendet.";
            _strings["nomai_init_error"]       = "Nomai-Textanzeige nicht verfügbar.";
        }

        #endregion
    }
}
