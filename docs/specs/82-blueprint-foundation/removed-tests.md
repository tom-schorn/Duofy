# Entfernte Tests von Duofy.Shared.Tests (Spec 82, A8)

Stand: Vergleich der Testmethoden von `main` mit `feat/v2-ui`. Auf `main` sind es 931 Testmethoden. 24 sind nur umbenannt und hier nicht aufgeführt. Theorien zählen als eine Methode.

Mit Ersatz: 127. Ersatzlos entfallen: 76.

## Ersatzlos entfallen, einzeln mit Grund

| Datei | Test | Grund |
| --- | --- | --- |
| AccessibilityMarkupTests | The_app_heading_is_not_drawn_in_the_browser | Eine eigene versteckte Überschrift nur für die App gibt es nicht mehr; App und Browser nutzen dieselbe Überschrift der Seite |
| AccessibilityMarkupTests | The_app_heading_stays_for_screen_readers_in_the_app | Eine eigene versteckte Überschrift nur für die App gibt es nicht mehr; App und Browser nutzen dieselbe Überschrift der Seite |
| AccessibilityStyleTests | The_app_bar_shows_the_focus_outline_in_its_own_text_colour | Die dunkle obere Leiste und die farbige Karte des Umschalters gibt es nicht mehr; Fokus und Farbe folgen dem Theme |
| AccessibilityStyleTests | The_profile_field_on_the_primary_card_shows_a_focus_outline_in_the_colour_on_primary | Die dunkle obere Leiste und die farbige Karte des Umschalters gibt es nicht mehr; Fokus und Farbe folgen dem Theme |
| AccessibilityStyleTests | No_text_in_a_heading_size_leaves_its_tag_to_the_default | prüfte nur die Bauart der alten Bibliothek |
| AppLayoutTests | In_the_browser_the_same_dialog_is_not_full_screen | App und Browser zeigen seit Spec 82 dieselben Seiten; kein Vollbild-Sonderweg mehr |
| AppLayoutTests | In_the_app_the_contract_dialog_is_the_step_dialog_with_cancel_on_top_and_next_below | App und Browser zeigen seit Spec 82 dieselben Seiten; kein Vollbild-Sonderweg mehr |
| AppLayoutTests | In_the_browser_the_contract_dialog_is_not_full_screen | App und Browser zeigen seit Spec 82 dieselben Seiten; kein Vollbild-Sonderweg mehr |
| AppLayoutTests | The_app_layout_is_built_from_radzen_parts | prüfte nur die Bauart der alten Bibliothek |
| AppLayoutTests | The_plus_button_is_the_radzen_fab_with_its_label | Toms Entscheidung 02.11.: kein runder Plus-Knopf |
| AppLayoutTests | The_destinations_only_name_material_symbols | prüfte nur die Bauart der alten Bibliothek |
| AuthLayoutTests | Other_pages_keep_app_bar_and_show_the_bottom_bar_in_the_app | App und Browser zeigen seit Spec 82 dieselben Seiten; kein Vollbild-Sonderweg mehr |
| AuthLayoutTests | Every_text_field_is_outlined_with_its_label_above | Der Rahmen kommt jetzt aus .field-input; das Aussehen ist im Bildvergleich belegt |
| BuildingBlockTests | Native_select_takes_its_colours_and_shape_from_radzen_variables | prüfte nur die Bauart der alten Bibliothek |
| BuildingBlocksTests | Color_block_shows_title_and_amount_on_the_surface_of_its_variant | Der alte Farbblock ist durch die schlichte Kennzahl ersetzt (Spec 82: keine Farbblöcke); die Inhalte prüfen MonthPageTests.The_three_blocks_show_income_allocatable_and_what_is_still_due und die Linientabelle MonthPageTests.The_bookings_tab_lists_all_seven_bookings_newest_first_with_date_and_amount |
| BuildingBlocksTests | Color_block_may_carry_a_note_under_the_amount | Der alte Farbblock ist durch die schlichte Kennzahl ersetzt (Spec 82: keine Farbblöcke); die Inhalte prüfen MonthPageTests.The_three_blocks_show_income_allocatable_and_what_is_still_due und die Linientabelle MonthPageTests.The_bookings_tab_lists_all_seven_bookings_newest_first_with_date_and_amount |
| BuildingBlocksTests | Color_block_may_show_its_own_text_instead_of_the_amount | Der alte Farbblock ist durch die schlichte Kennzahl ersetzt (Spec 82: keine Farbblöcke); die Inhalte prüfen MonthPageTests.The_three_blocks_show_income_allocatable_and_what_is_still_due und die Linientabelle MonthPageTests.The_bookings_tab_lists_all_seven_bookings_newest_first_with_date_and_amount |
| BuildingBlocksTests | Every_block_has_an_accessible_name | Prüfte die Testseite /bausteine; die Namen der Bausteine prüfen AccessibilityMarkupTests und StepDialogFrameTests |
| BuildingBlocksTests | Test_page_shows_all_blocks_in_a_light_and_a_dark_panel_in_development | Die Entwicklerseiten /bausteine und /diagramme bleiben als Route (InterfaceRulesTests.A7_every_route_of_main_still_exists), zeigen aber nicht mehr zwei Farbfelder nebeneinander; der dunkle Modus gilt jetzt für die ganze Seite |
| ChartBlockTests | Test_page_shows_all_charts_in_a_light_and_a_dark_panel_in_development | Die Entwicklerseiten /bausteine und /diagramme bleiben als Route (InterfaceRulesTests.A7_every_route_of_main_still_exists), zeigen aber nicht mehr zwei Farbfelder nebeneinander; der dunkle Modus gilt jetzt für die ganze Seite |
| DesktopWidthTests | The_start_page_is_wide_and_named_after_the_brand | Alle Seiten nutzen dieselbe Breite des alten Duofy; Bilder bei 1440 Pixeln belegen es |
| DesktopWidthTests | The_month_review_puts_its_two_charts_in_columns | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | Only_pages_marked_as_wide_use_the_larger_content_width | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | The_columns_exist_only_from_1280px | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | The_table_heads_are_hidden_below_1280px_and_hidden_from_screen_readers | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | Accounts_are_a_table_with_columns | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | Contracts_are_a_table_and_their_line_reads_as_before | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | Bookings_are_a_table_with_their_controls_in_one_row | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | Household_shows_card_and_members_in_columns | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | The_list_of_months_is_marked_as_wide | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | The_month_overview_puts_its_sections_in_columns | Toms Entscheidung 02.11.: kein Reiter Übersicht |
| DesktopWidthTests | The_plan_tab_has_positions_beside_sums_and_carry_over | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| DesktopWidthTests | A_line_table_row_keeps_its_text_and_has_a_wrapper_for_the_columns | Die Tabellen und Spalten ab 1280 Pixeln waren das alte Layout; Spec 82 baut Listen und Spalten wie im alten Duofy, belegt durch den Bildvergleich |
| FontInterTests | No_geist_files_or_references_remain | Spec 82 wechselt von Inter zu Geist |
| FormDialogsTests | In_the_app_the_dialog_fills_the_screen_and_draws_no_title_of_its_own | App und Browser zeigen seit Spec 82 dieselben Seiten; kein Vollbild-Sonderweg mehr |
| FormDialogsTests | In_the_app_a_click_next_to_the_form_does_not_close_it_by_accident | App und Browser zeigen seit Spec 82 dieselben Seiten; kein Vollbild-Sonderweg mehr |
| LicensesPageTests | The_page_is_built_with_radzen_only | prüfte nur die Bauart der alten Bibliothek |
| NarrowLayoutStyleTests | The_cancel_button_keeps_its_word_in_one_piece | Sonderregel für Wortumbrüche der alten Knöpfe; Bilder bei 360 Pixeln zeigen die Wörter ganz. Bei sehr langen Wörtern nicht eigens geprüft |
| NarrowLayoutStyleTests | The_plus_button_sits_above_the_bottom_bar | Toms Entscheidung 02.11.: kein runder Plus-Knopf |
| NarrowLayoutStyleTests | A_bar_label_keeps_a_word_like_grundbedarf_in_one_piece | Sonderregel für Wortumbrüche der alten Knöpfe; Bilder bei 360 Pixeln zeigen die Wörter ganz. Bei sehr langen Wörtern nicht eigens geprüft |
| RadzenPlanSlicesTests | The_page_sets_the_base_font_itself | Die Grundschrift steht jetzt in html und body in duofy.css; Aussehen im Bildvergleich |
| RazorFilesTests | Inline_styles_exist_only_where_the_value_is_known_at_run_time | Die Regel galt dem alten Stylesheet und wurde beim Umbau nicht übertragen; A3 prüft die festen Farben. Eine Sperre gegen Inline-Stile fehlt jetzt, das ist eine bewusste Lücke |
| ResponsiveScaffoldTests | The_documentation_repeats_the_three_ranges | docs/layout.md nennt noch 1280 Pixel und liegt außerhalb des Auftrags; an die Leitung gemeldet |
| ResponsiveScaffoldTests | Content_is_limited_to_1180px_and_centred_from_1280px | Maße des alten Seitenrahmens; der neue Rahmen AppShell gilt für App und Browser und ist im Bildvergleich belegt |
| ResponsiveScaffoldTests | Below_desktop_the_content_fills_the_width_with_a_side_gutter | Maße des alten Seitenrahmens; der neue Rahmen AppShell gilt für App und Browser und ist im Bildvergleich belegt |
| ResponsiveScaffoldTests | The_main_pages_are_drawn_inside_the_page_frame | Maße des alten Seitenrahmens; der neue Rahmen AppShell gilt für App und Browser und ist im Bildvergleich belegt |
| ResponsiveScaffoldTests | The_gutter_and_block_gap_are_defined_once_per_range | Maße des alten Seitenrahmens; der neue Rahmen AppShell gilt für App und Browser und ist im Bildvergleich belegt |
| ResponsiveScaffoldTests | Both_layouts_use_the_page_frame_instead_of_their_own_margins | Maße des alten Seitenrahmens; der neue Rahmen AppShell gilt für App und Browser und ist im Bildvergleich belegt |
| ResponsiveScaffoldTests | The_type_scale_of_the_theme_is_what_the_frame_uses_on_desktop | Die Schriftstufen des alten Rahmens gelten nicht mehr; Größen wie im alten Duofy, im Bildvergleich belegt |
| ResponsiveScaffoldTests | The_section_size_grows_from_phone_over_tablet_to_desktop | Die Schriftstufen des alten Rahmens gelten nicht mehr; Größen wie im alten Duofy, im Bildvergleich belegt |
| ResponsiveScaffoldTests | The_frame_rules_take_their_font_sizes_from_the_scale | Die Schriftstufen des alten Rahmens gelten nicht mehr; Größen wie im alten Duofy, im Bildvergleich belegt |
| ResponsiveScaffoldTests | The_title_in_the_app_bar_uses_the_section_role | Die Schriftstufen des alten Rahmens gelten nicht mehr; Größen wie im alten Duofy, im Bildvergleich belegt |
| SideNavigationTests | The_web_layout_is_built_from_radzen_parts | prüfte nur die Bauart der alten Bibliothek |
| SideNavigationTests | The_radzen_side_bar_stays_out_of_the_way_below_the_desktop | prüfte nur die Bauart der alten Bibliothek |
| SideNavigationTests | Side_entries_are_at_least_44px_high | Die Seitenleiste steht nur am PC mit Maus; ihre Höhe folgt dem alten Duofy |
| ThemeFirstPaintTests | The_head_style_uses_the_dark_colours_of_the_theme_under_the_class | Die alte Variablen-Technik ist entfernt. Dunkel beim ersten Zeichnen setzt jetzt die Klasse aus dem frühen Skript (ThemeBoot, index.html), geprüft bleiben die übrigen Fälle in ThemeFirstPaintTests; im Browser ohne Aufblitzen nicht eigens gemessen |
| ThemeFirstPaintTests | The_layout_drops_the_early_class_once_it_draws_in_the_right_colour_itself | Die alte Variablen-Technik ist entfernt. Dunkel beim ersten Zeichnen setzt jetzt die Klasse aus dem frühen Skript (ThemeBoot, index.html), geprüft bleiben die übrigen Fälle in ThemeFirstPaintTests; im Browser ohne Aufblitzen nicht eigens gemessen |
| ThemeFirstPaintTests | A_store_that_knows_the_choice_at_once_makes_the_layout_draw_it_in_the_first_render | Die alte Variablen-Technik ist entfernt. Dunkel beim ersten Zeichnen setzt jetzt die Klasse aus dem frühen Skript (ThemeBoot, index.html), geprüft bleiben die übrigen Fälle in ThemeFirstPaintTests; im Browser ohne Aufblitzen nicht eigens gemessen |
| ThemeModeTests | The_switch_keeps_its_tap_targets_and_the_colour_of_the_top_bar | Die dunkle obere Leiste und die farbige Karte des Umschalters gibt es nicht mehr; Fokus und Farbe folgen dem Theme |
| ThemePaletteTests | Type_scale_names_size_weight_and_line_height_for_every_role | Die Schriftstufen des alten Themes gelten nicht mehr |
| ThemeVariablesTests | Progress_bars_get_their_height_and_corners_from_the_theme | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_menu_has_no_dividers_and_no_bar_on_the_active_item | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | Outlined_cards_have_a_visible_frame_in_both_modes | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | Lines_of_inputs_reach_3_to_1 | Abweichung: Spec 82 A2 verlangt die Werte des alten Duofy, dort erreicht --input nur etwa 1,4 zu 1 statt 3 zu 1. Im Bericht als Abweichung genannt |
| ThemeVariablesTests | The_border_of_checkboxes_and_radios_uses_the_line_of_the_inputs | Abweichung: Spec 82 A2 verlangt die Werte des alten Duofy, dort erreicht --input nur etwa 1,4 zu 1 statt 3 zu 1. Im Bericht als Abweichung genannt |
| ThemeVariablesTests | The_style_overrides_the_theme_file_whatever_the_order | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_style_holds_common_and_mode_values | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_component_draws_the_style_of_the_mode | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_style_follows_a_change_of_the_mode_without_a_new_component | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_browser_layout_draws_the_variables | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | Every_layout_file_names_the_variables | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_theme_file_comes_from_radzen_theme_on_the_web_page_and_in_the_app | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | Radzen_theme_draws_the_link_of_the_light_theme | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_first_paint_style_carries_the_dark_values_and_outweighs_the_light_style | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | Every_variable_is_one_the_theme_file_knows | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |
| ThemeVariablesTests | The_menu_variables_are_really_used_by_the_theme_file | Prüfte die Variablen der alten Bibliothek; die Werte stehen jetzt in theme.css (ThemeFileTests) und die Modus-Klasse setzt ThemeVariables (ThemeSwitchTests) |

## Mit Ersatz

| Datei | Test | Ersatz |
| --- | --- | --- |
| AccessibilityMarkupTests | The_page_title_is_a_main_heading_in_the_browser_and_in_the_app | InterfaceBehaviourTests.The_page_title_is_the_only_main_heading_and_the_top_bar_title_is_none |
| AccessibilityMarkupTests | The_app_layout_has_the_skip_link_and_its_target_too | InterfaceBehaviourTests.The_shell_has_the_skip_link_and_its_target |
| AccessibilityMarkupTests | A_required_field_is_marked_and_turns_invalid_after_next | InterfaceBehaviourTests.A_required_field_stops_next_and_names_its_error_under_the_field |
| AccessibilityStyleTests | Links_and_buttons_show_the_focus_outline | InterfaceRulesTests.Everything_that_takes_the_focus_shows_it |
| AccessibilityStyleTests | The_back_link_of_the_plan_page_is_a_tap_target | InterfaceRulesTests.Below_the_desktop_everything_to_tap_is_at_least_44_px |
| AccessibilityStyleTests | No_text_or_unfilled_button_is_drawn_in_a_light_status_colour | InterfaceRulesTests.Text_reaches_4_5_to_1 |
| AccessibilityStyleTests | The_app_can_be_zoomed | InterfaceRulesTests.The_pages_can_be_zoomed |
| AppLayoutTests | Plan_shows_the_month_and_its_plus_button_adds_a_position | AppLayoutTests.Plan_shows_the_month_and_its_head_button_adds_a_position |
| AppLayoutTests | The_plus_button_opens_the_account_dialog_on_konten | AppLayoutTests.The_head_button_opens_the_account_dialog_on_konten |
| AppLayoutTests | The_plus_button_on_start_opens_the_booking_dialog | AppLayoutTests.The_booking_button_on_start_opens_the_booking_dialog |
| AppLayoutTests | Pages_without_a_main_action_show_no_plus_button | AppLayoutTests.No_page_shows_a_round_plus_button |
| AppLayoutTests | Leaving_a_page_with_a_plus_button_takes_the_button_away | AppLayoutTests.Leaving_a_page_changes_the_title_of_the_top_bar |
| AppLayoutTests | The_plus_button_opens_the_contract_dialog_on_vertraege | AppLayoutTests.The_head_button_opens_the_contract_dialog_on_vertraege |
| AppLayoutTests | Vertraege_show_their_title_in_the_top_bar_only_once | AppLayoutTests.Vertraege_show_their_title_in_the_top_bar_and_their_heading_and_button_in_the_page |
| AppLayoutTests | Konten_show_their_title_in_the_top_bar_and_the_bar_has_no_menu_button | AppLayoutTests.Leaving_a_page_changes_the_title_of_the_top_bar |
| AppLayoutTests | The_bottom_bar_marks_the_open_page | SideNavigationTests.The_bottom_bar_marks_the_right_target |
| AppLayoutTests | Bottom_bar_and_plus_button_reserve_the_bottom_safe_area_inset | InterfaceRulesTests.The_bars_reserve_the_safe_areas_of_the_phone |
| AppLayoutTests | The_layout_renders_the_elements_the_safe_area_styles_belong_to | InterfaceRulesTests.The_bars_reserve_the_safe_areas_of_the_phone |
| AppLayoutTests | The_member_switch_sits_in_the_top_bar_whether_or_not_somebody_shares | InterfaceBehaviourTests.The_member_switch_stands_in_the_top_bar_and_in_the_side_bar |
| AppLayoutTests | The_start_plus_is_greyed_out_with_the_reason_while_anna_does_not_let_ben_book | AppLayoutTests.The_start_button_is_greyed_out_with_the_reason_while_anna_does_not_let_ben_book |
| BrandTests | The_app_bar_of_the_browser_shows_the_plagevo_wordmark_in_light_mode | BrandTests.The_side_bar_of_the_browser_shows_the_plagevo_wordmark_in_light_mode |
| BrandTests | The_app_bar_of_the_browser_shows_the_dark_wordmark_in_dark_mode | BrandTests.The_side_bar_of_the_browser_shows_the_dark_wordmark_in_dark_mode |
| BrandTests | The_app_bar_of_the_app_shows_the_plagevo_symbol_before_the_title_of_the_page | BrandTests.The_top_bar_of_the_app_shows_the_plagevo_symbol_before_the_title_of_the_page |
| BuildingBlockTests | Notice_shows_its_text_as_a_radzen_alert_of_the_matching_kind | InterfaceBehaviourTests.A_notice_shows_its_text_as_an_alert, InterfaceBehaviourTests.The_kinds_of_a_notice_look_different |
| BuildingBlockTests | Notice_without_a_message_shows_nothing | InterfaceBehaviourTests.A_notice_without_a_message_shows_nothing |
| BuildingBlocksTests | Line_table_shows_label_and_amount_per_row_separated_by_lines | InterfaceBehaviourTests.A_line_table_shows_label_and_amount_per_row_and_is_named |
| BuildingBlocksTests | Line_table_rows_may_carry_leading_detail_and_trailing_content | InterfaceBehaviourTests.A_line_table_row_may_carry_detail_and_trailing_content |
| BuildingBlocksTests | Button_has_a_kind_and_a_visible_3px_ink_focus_outline | InterfaceRulesTests.Everything_that_takes_the_focus_shows_it (2 Pixel wie im alten Duofy statt 3) |
| BuildingBlocksTests | Every_text_and_background_pair_of_the_blocks_reaches_4_5_to_1_in_light_and_dark | InterfaceRulesTests.Text_reaches_4_5_to_1 |
| ChartBlockTests | Target_actual_shows_125_percent_as_text_without_an_error_colour | MonthPageTests.The_gap_chart_shows_planned_against_actual_in_euro_without_an_alarm_colour; über der Quote ist Rot im alten Duofy gewollt (Spec 82, R8) |
| ChartBlockTests | Chart_styles_take_colours_only_from_theme_variables | InterfaceRulesTests.A3_fixed_colours_stand_only_in_the_theme_file |
| DesktopWidthTests | The_stylesheet_has_balanced_braces | InterfaceRulesTests.The_stylesheets_have_balanced_braces |
| DesktopWidthTests | Start_boxes_and_month_tabs_stay_inside_a_narrow_screen | Messung im Browser: kein Überstand auf allen Seiten bei 360, 768, 1440 Pixeln (walk.log) |
| FontInterTests | Theme_uses_inter_as_first_font_family | InterfaceRulesTests.The_font_comes_from_our_own_file_with_its_licence_and_source_next_to_it |
| FontInterTests | Css_declares_inter_as_font_face_from_a_local_file | InterfaceRulesTests.The_font_comes_from_our_own_file_with_its_licence_and_source_next_to_it |
| FontInterTests | Font_list_ends_in_a_system_fallback | InterfaceRulesTests.The_font_comes_from_our_own_file_with_its_licence_and_source_next_to_it |
| FontInterTests | Css_loads_nothing_from_another_host | InterfaceRulesTests.The_stylesheets_load_nothing_from_another_host |
| FontInterTests | Inter_license_lies_next_to_the_font_and_docs_name_source_and_version | InterfaceRulesTests.The_font_comes_from_our_own_file_with_its_licence_and_source_next_to_it |
| FontInterTests | Css_sets_tabular_figures | InterfaceRulesTests.Amounts_use_figures_of_the_same_width |
| FontInterTests | Body_text_uses_the_theme_font_family | ThemeFileTests (--font-sans) und InterfaceRulesTests.The_font_comes_from_our_own_file_with_its_licence_and_source_next_to_it |
| FormDialogsTests | In_the_browser_the_dialog_is_a_window_in_the_middle_with_title_and_close_button | InterfaceBehaviourTests.Closing_a_window_dialog_with_the_x_counts_as_nothing_changed |
| FormDialogsTests | Esc_closes_the_dialog_and_radzen_does_not_pick_the_focus_itself | InterfaceBehaviourTests.A12_escape_closes_a_step_dialog_from_inside_its_fields |
| FormDialogsTests | Every_dialog_asks_the_browser_to_focus_its_first_field_after_opening | InterfaceBehaviourTests.A12_a_dialog_takes_the_focus_and_gives_it_back_to_the_trigger |
| FormDialogsTests | Closing_with_the_x_counts_as_nothing_changed | InterfaceBehaviourTests.Closing_a_window_dialog_with_the_x_counts_as_nothing_changed |
| FormDialogsTests | A_window_dialog_stays_a_window_in_the_app_and_has_a_title_and_close_button | InterfaceBehaviourTests.Closing_a_window_dialog_with_the_x_counts_as_nothing_changed |
| GrantedPagesTests | With_plan_view_only_the_overview_says_not_shared_instead_of_list_gap_and_open_amounts | GrantedPagesTests.With_plan_view_only_review_bookings_and_unpaid_say_not_shared_instead_of_wrong_amounts |
| GrantedPagesTests | With_bookings_view_the_overview_shows_the_gap_and_the_open_amounts | GrantedPagesTests.With_bookings_view_the_review_shows_the_gap_and_the_open_amounts |
| LicensesPageTests | The_page_shows_the_notices_of_radzen_inter_and_material_symbols | InterfaceBehaviourTests.The_licence_page_shows_the_notice_of_every_new_building_block, InterfaceRulesTests.The_licence_list_has_the_copyright_line_of_the_source, InterfaceRulesTests.The_licence_list_names_nothing_that_is_gone |
| LicensesPageTests | Radzen_is_pinned_to_the_version_the_plan_names | InterfaceRulesTests.The_licence_list_names_the_pinned_version |
| LicensesPageTests | The_ui_registration_adds_the_radzen_services | InterfaceRulesTests.The_licence_list_names_the_pinned_version |
| LinkButtonTests | It_is_a_link_with_the_classes_of_a_radzen_button_and_passes_attributes_on | InvitationPageTests.The_page_is_built_from_the_blocks_and_its_sign_in_and_register_links_look_like_buttons, InterfaceRulesTests.A_primary_button_that_is_a_link_keeps_its_light_text |
| LinkButtonTests | The_kind_and_the_width_follow_the_parameters | InvitationPageTests.The_page_is_built_from_the_blocks_and_its_sign_in_and_register_links_look_like_buttons, InterfaceRulesTests.A_primary_button_that_is_a_link_keeps_its_light_text |
| MainLayoutTests | Layout_contains_theme_provider_with_duofy_theme_app_bar_and_side_navigation | AppLayoutTests.The_browser_keeps_main_layout_with_side_bar_and_bottom_bar_and_no_drawer |
| MemberSwitchTests | The_menu_offers_account_and_sign_out_after_the_views | MemberSwitchTests.The_menu_offers_only_the_views_and_no_account_or_sign_out |
| MemberSwitchTests | Signing_out_from_the_menu_logs_out_and_goes_to_the_sign_in_page | SideNavigationTests.Sign_out_in_the_side_bar_signs_out_and_leads_to_the_sign_in |
| MemberSwitchTests | The_menu_closes_with_escape_and_with_a_second_tap | MemberSwitchTests.The_menu_closes_with_a_second_tap; Esc wird im Browser gedrückt, das Schließen danach ist nicht eigens gemessen |
| MemberSwitchTests | The_field_is_the_radzen_profile_menu_and_names_the_view | SideNavigationTests.The_side_bar_has_the_member_switch_on_top_and_the_user_menu_at_the_bottom_even_when_alone |
| MemberSwitchTests | At_the_bottom_of_the_side_bar_the_field_sits_on_a_card_and_says_what_it_is_for | SideNavigationTests.The_side_bar_has_the_member_switch_on_top_and_the_user_menu_at_the_bottom_even_when_alone |
| MoneyAndEnterInputTests | Every_decimal_field_names_its_culture | MoneyAndEnterInputTests.Every_amount_field_is_the_german_money_input |
| MonthPageTests | The_month_opens_with_the_tabs_overview_plan_and_bookings_and_the_overview_is_active | MonthPageTests.The_month_opens_with_the_tabs_plan_bookings_and_review_and_the_plan_is_active |
| MonthPageTests | The_three_blocks_show_income_fixed_and_free | MonthPageTests.The_three_blocks_show_income_allocatable_and_what_is_still_due (Festes entfällt nach Spec 82) |
| MonthPageTests | The_overview_lists_only_the_five_newest_of_seven_bookings_with_date_and_amount | MonthPageTests.The_bookings_tab_lists_all_seven_bookings_newest_first_with_date_and_amount (Toms Entscheidung 02.11.: kein Reiter Übersicht) |
| MonthPageTests | The_quick_actions_are_new_booking_new_position_and_tick_a_position | MonthPageTests.The_page_head_offers_new_booking_and_new_position; "Posten abhaken" entfällt, das Kästchen steht am Posten |
| MonthPageTests | New_booking_and_new_position_open_their_dialogs_and_tick_leads_to_the_plan_tab | MonthPageTests.New_booking_opens_its_dialog_and_cancelling_leaves_the_month_as_it_is |
| MonthPageTests | The_plan_tab_groups_positions_by_pot_in_line_tables_and_they_can_be_ticked | MonthPageTests.The_plan_tab_groups_positions_by_pot_with_amount_tick_box_unplanned_line_and_sankey |
| MonthPageTests | The_blocks_stand_side_by_side_from_600_px_and_below_each_other_under_it | MonthPageTests.The_blocks_stand_side_by_side_and_below_each_other_only_on_narrow_screens |
| MonthPageTests | The_household_overview_shows_blocks_gaps_recent_bookings_and_due_payments_without_actions | MonthPageTests.The_household_month_shows_blocks_gaps_bookings_and_due_payments_without_actions |
| MonthPageTests | Overview_bookings_tab_and_plan_tab_show_the_carry_overs_as_their_own_lines | MonthPageTests.Bookings_tab_and_plan_tab_show_the_carry_overs_as_their_own_lines |
| MonthPageTests | The_household_overview_names_missing_members_instead_of_saying_there_are_no_bookings | MonthPageTests.The_household_bookings_name_missing_members_instead_of_saying_there_are_no_bookings |
| MonthPageTests | The_household_overview_shows_the_visible_bookings_and_still_names_missing_members | MonthPageTests.The_household_bookings_show_the_visible_ones_and_still_name_missing_members |
| MonthPageTests | The_household_overview_says_there_are_no_bookings_when_nobody_is_missing | MonthPageTests.The_household_bookings_say_there_are_none_when_nobody_is_missing |
| MonthPageTests | The_household_overview_names_members_whose_recent_bookings_are_not_released | MonthPageTests.The_household_bookings_show_the_visible_ones_and_still_name_missing_members |
| MonthPageTests | The_household_overview_does_not_claim_an_empty_month_while_bookings_are_not_released | MonthPageTests.The_household_bookings_name_missing_members_instead_of_saying_there_are_no_bookings |
| MonthPageTests | Without_missing_names_and_for_a_single_person_the_overview_has_no_notice | MonthPageTests.Without_missing_names_and_for_a_single_person_the_bookings_have_no_notice |
| MonthReviewTests | The_month_offers_a_fourth_tab_review_after_bookings | MonthReviewTests.The_month_offers_the_tab_review_after_bookings |
| NarrowLayoutStyleTests | A_line_table_row_wraps_its_trailing_cell_on_a_narrow_phone | InterfaceRulesTests.The_frame_lets_its_content_shrink_so_the_page_never_gets_wider_than_the_window; Messung ohne Überstand (walk.log) |
| NarrowLayoutStyleTests | All_bottom_bar_labels_fit_on_a_narrow_phone | InterfaceRulesTests.Below_the_desktop_the_side_bar_is_hidden_and_the_bottom_bar_shown; Bilder bei 360 Pixeln |
| NarrowLayoutStyleTests | Row_buttons_wrap_instead_of_leaving_the_screen_on_a_narrow_phone | InterfaceRulesTests.The_frame_lets_its_content_shrink_so_the_page_never_gets_wider_than_the_window; Messung ohne Überstand (walk.log) |
| NarrowLayoutStyleTests | The_tick_box_keeps_its_own_width_on_a_narrow_phone | InterfaceRulesTests.Below_the_desktop_everything_to_tap_is_at_least_44_px |
| NarrowLayoutStyleTests | The_consent_banner_covers_neither_navigation_nor_profile_button | InterfaceRulesTests.The_consent_banner_stays_above_the_bottom_bar; das Feld liegt jetzt schwebend wie im Umbau gewählt, nicht mehr im Fluss |
| NarrowLayoutStyleTests | The_consent_banner_sits_in_the_flow_and_its_buttons_are_at_least_44_px_high | InterfaceRulesTests.The_consent_banner_stays_above_the_bottom_bar; das Feld liegt jetzt schwebend wie im Umbau gewählt, nicht mehr im Fluss |
| NarrowLayoutStyleTests | A_movement_keeps_its_amount_and_both_buttons_together | InterfaceRulesTests.The_frame_lets_its_content_shrink_so_the_page_never_gets_wider_than_the_window; Messung ohne Überstand (walk.log) |
| PaymentMethodSymbolsTests | The_style_puts_the_symbol_above_the_text_and_marks_the_chosen_option_not_by_colour_alone_and_shows_the_focus | PaymentMethodSymbolsTests.The_style_marks_the_chosen_option_not_by_colour_alone_and_shows_the_focus (Symbol steht neben dem Text statt darüber) |
| PlanPageTests | Ticking_a_position_without_bookings_books_it_at_once | MonthPageTests.The_tick_box_opens_a_dialog_with_date_and_amount_and_confirming_books_exactly_once (Spec 82 ändert das Verhalten) |
| PlanPageTests | Ticking_a_position_with_bookings_offers_the_open_rest_as_balance | PlanPageTests.Ticking_a_position_with_bookings_creates_no_further_booking (Spec 82 ändert das Verhalten) |
| RadzenPlanSlicesTests | Radzen_buttons_in_the_content_are_tap_targets_of_44_px | InterfaceRulesTests.Below_the_desktop_everything_to_tap_is_at_least_44_px |
| RadzenPlanSlicesTests | Framed_boxes_have_the_rounded_corners_of_the_theme | ThemeFileTests (--radius) |
| RazorFilesTests | The_stylesheet_has_no_fixed_colour_except_the_white_of_the_qr_code | InterfaceRulesTests.A3_fixed_colours_stand_only_in_the_theme_file |
| RazorFilesTests | Detector_finds_text_between_tags_and_in_attributes | RazorFilesTests.Razor_files_of_shared_contain_no_literal_text prüft den Echtbestand; der Selbsttest des Filters ist ersatzlos entfallen, weil seine Beispiele Bauteile der alten Bibliothek waren |
| ResponsiveScaffoldTests | Breakpoints_are_defined_once_as_phone_to_599_tablet_600_to_1279_desktop_from_1280 | InterfaceRulesTests.The_ranges_are_phone_to_599_tablet_to_1023_and_desktop_from_1024_without_a_gap |
| ResponsiveScaffoldTests | The_stylesheet_only_breaks_at_the_defined_breakpoints | InterfaceRulesTests.The_stylesheet_only_breaks_at_the_defined_widths |
| ResponsiveScaffoldTests | Tap_targets_are_at_least_44px_high_and_wide | InterfaceRulesTests.Below_the_desktop_everything_to_tap_is_at_least_44_px |
| ResponsiveScaffoldTests | The_bottom_bar_keeps_its_four_destinations | SideNavigationTests.The_bottom_bar_has_the_four_targets_of_the_human |
| ResponsiveScaffoldTests | The_frame_has_a_rule_against_horizontal_overflow_and_no_width_above_100_percent | InterfaceRulesTests.The_frame_lets_its_content_shrink_so_the_page_never_gets_wider_than_the_window |
| ServerChoiceTests | In_the_app_the_dialog_is_a_full_screen_radzen_dialog_with_its_own_header_and_cancel_changes_nothing | ServerChoiceTests.In_the_app_the_dialog_is_the_same_window_and_closing_changes_nothing |
| SideNavigationTests | From_1280px_the_side_bar_is_shown_and_the_bottom_bar_is_hidden | InterfaceRulesTests.Below_the_desktop_the_side_bar_is_hidden_and_the_bottom_bar_shown (Grenze 1.024, von Tom bestätigt) |
| SideNavigationTests | The_side_bar_shows_admin_only_to_admins | SideNavigationTests.The_user_menu_shows_admin_only_to_admins, SideNavigationTests.The_user_menu_leads_admins_to_the_administration |
| SideNavigationTests | The_side_bar_lists_the_legal_texts_of_the_server | SideNavigationTests.The_user_menu_lists_the_legal_texts_of_the_server |
| SideNavigationTests | The_app_shows_the_mode_switch_in_the_side_bar | SideNavigationTests.The_app_shows_the_three_way_theme_switch_in_the_top_bar |
| SideNavigationTests | The_browser_keeps_the_mode_switch_in_the_top_bar_only | SideNavigationTests.The_browser_keeps_the_theme_switch_in_the_top_bar_only |
| SideNavigationTests | The_entries_scroll_and_the_footer_stays | InterfaceRulesTests.The_entries_of_the_side_bar_scroll |
| SideNavigationTests | The_open_page_is_marked_in_the_side_bar | SideNavigationTests.Sub_pages_mark_their_entry |
| SideNavigationTests | All_entries_are_links_in_list_order_and_keep_the_focus_outline | SideNavigationTests.The_side_bar_shows_everything_directly_and_no_more_entry, InterfaceRulesTests.Everything_that_takes_the_focus_shows_it |
| SideNavigationTests | The_web_layout_keeps_the_member_switch_in_the_app_bar_when_somebody_shares | InterfaceBehaviourTests.The_member_switch_stands_in_the_top_bar_and_in_the_side_bar |
| SideNavigationTests | The_web_layout_puts_the_profile_button_at_the_bottom_of_the_side_navigation_even_when_alone | SideNavigationTests.The_side_bar_has_the_member_switch_on_top_and_the_user_menu_at_the_bottom_even_when_alone |
| SideNavigationTests | A_long_name_is_shortened_in_the_top_bar | InterfaceRulesTests.A_long_name_is_shortened_in_the_switch |
| SideNavigationTests | The_stylesheet_hides_the_top_bar_profile_on_desktop_and_opens_the_side_menu_upwards | UserMenu öffnet laut Code mit Side=Top, im Browser nicht eigens gemessen. Das Ausblenden unter 1.024 prüft InterfaceRulesTests.Below_the_desktop_the_side_bar_is_hidden_and_the_bottom_bar_shown |
| StepDialogRadzenTests | The_dialog_is_a_radzen_dialog_that_draws_its_own_head_with_title_progress_and_cancel | StepDialogFrameTests.The_dialog_window_is_named_by_the_heading_of_the_step_dialog |
| StepDialogRadzenTests | A_numeric_field_is_labelled_through_aria_labelledby_because_radzen_gives_its_input_no_id | InterfaceBehaviourTests.A_money_field_in_a_dialog_is_named_by_its_label |
| StepDialogRadzenTests | The_options_close_only_through_the_dialogs_own_cancel | InterfaceBehaviourTests.A12_escape_closes_a_step_dialog_from_inside_its_fields |
| StepDialogRadzenTests | The_button_is_a_radzen_button_that_keeps_its_kind_classes_testid_and_label | StepDialogTests.Second_of_three_steps_shows_its_position_and_back_left_next_right |
| ThemeDarkTests | Main_layout_opens_in_dark_mode_with_the_dark_palette | ThemeFileTests und InterfaceBehaviourTests.An_explicit_choice_ignores_a_change_of_the_device |
| ThemeFirstPaintTests | The_app_start_page_has_a_dark_background_for_a_dark_device_before_the_layout_draws | InterfaceRulesTests.Dark_is_set_before_the_first_paint |
| ThemeModeTests | The_layout_asks_radzen_for_the_device | InterfaceBehaviourTests.With_device_the_display_follows_a_change_of_the_device |
| ThemeModeTests | The_switch_is_drawn_with_radzen | ThemeSwitchTests.A1_the_switch_offers_light_dark_and_device_and_exactly_one_is_active |
| ThemePaletteTests | Css_draws_a_3px_focus_outline | InterfaceRulesTests.Everything_that_takes_the_focus_shows_it (2 Pixel wie im alten Duofy statt 3) |
| ThemeSwitchTests | A4_with_device_the_display_follows_a_change_of_the_device | InterfaceBehaviourTests.With_device_the_display_follows_a_change_of_the_device |
| ThemeSwitchTests | A4_an_explicit_choice_ignores_a_change_of_the_device | InterfaceBehaviourTests.An_explicit_choice_ignores_a_change_of_the_device |
| ThemeSwitchTests | A4_switching_back_to_device_uses_the_latest_device_setting | InterfaceBehaviourTests.Switching_back_to_device_uses_the_latest_device_setting |
| ThemeVariablesTests | Light_has_the_palette_altes_duofy | ThemeFileTests (A2) |
| ThemeVariablesTests | Dark_has_the_confirmed_design | ThemeFileTests (A2) |
| ThemeVariablesTests | Every_corner_is_12_px | ThemeFileTests (A2) |
| ThemeVariablesTests | Dialogs_lie_above_the_top_bar_and_the_bottom_bar | InterfaceRulesTests.The_bars_lie_below_the_dialogs |
| ThemeVariablesTests | The_text_font_is_inter_with_a_system_fallback | ThemeFileTests (A2) |
| ThemeVariablesTests | No_default_pink_is_left | ThemeFileTests (A2) |
| ThemeVariablesTests | No_dark_surface_is_pure_black | ThemeFileTests (A2) |
| ThemeVariablesTests | Inter_comes_from_our_own_files | InterfaceRulesTests.The_font_comes_from_our_own_file_with_its_licence_and_source_next_to_it |

## Änderung nach Toms Durchsicht vom 03.11.2026 (R11 bis R20)

Vergleich der Testmethoden von Commit 8d8b28a (letzter Stand dieser Liste) mit dem Stand nach dem Druckbild. 31 Methoden sind ersetzt, keine ist ersatzlos entfallen. Wo oben ein Ersatz genannt ist, der hier als alter Test steht, gilt der Ersatz aus dieser Tabelle.

| Datei | Alter Test | Ersatz | Grund |
| --- | --- | --- | --- |
| AccessibilityMarkupTests | The_title_in_the_app_bar_is_no_heading | AccessibilityMarkupTests.The_top_bar_has_no_title_and_no_heading | Die obere Leiste nennt keinen Titel mehr (R11) |
| AppLayoutTests | Accounts_open_inside_the_app_layout_with_four_entries_and_more_highlighted | AppLayoutTests.Accounts_open_inside_the_app_layout_with_five_entries_and_more_highlighted | Untere Leiste mit Sparen (R12) |
| AppLayoutTests | Leaving_a_page_changes_the_title_of_the_top_bar | AppLayoutTests.A13_every_page_names_its_title_exactly_once | Kein Titel in der oberen Leiste (R11) |
| AppLayoutTests | Mehr_lists_konten_vertraege_konto_the_mode_switch_and_sign_out | AppLayoutTests.Mehr_lists_the_profile_konten_vertraege_konto_and_sign_out_but_no_household_and_no_mode_switch | Mehr mit Profil, ohne Haushalt und Umschalter (R11, R12) |
| AppLayoutTests | The_mode_switch_on_mehr_turns_dark_mode_on | AppLayoutTests.The_one_theme_toggle_of_the_app_turns_dark_mode_on | Ein Knopf für hell und dunkel (R11) |
| AppLayoutTests | Vertraege_show_their_title_in_the_top_bar_and_their_heading_and_button_in_the_page | AppLayoutTests.Vertraege_show_their_heading_and_button_in_the_page | Kein Titel in der oberen Leiste (R11) |
| AuthLayoutTests | A1_the_auth_pages_offer_light_dark_and_device_with_exactly_one_active | AuthLayoutTests.A1_the_auth_pages_offer_the_one_theme_toggle | Ein Knopf für hell und dunkel (R11) |
| HouseholdPageTests | Mehr_leads_to_the_household | HouseholdPageTests.The_household_has_its_target_in_the_bottom_bar_and_none_under_mehr | Mehr ohne Haushalt-Eintrag (R12) |
| HouseholdPlanPageTests | The_arrows_stay_in_the_household_plan | HouseholdPlanPageTests.The_household_month_leads_back_to_the_months, HouseholdPlanPageTests.A_month_of_the_household_opens_at_its_address | Kein Monatsumschalter im Monat (R15, R16) |
| HouseholdPlanPageTests | The_household_plan_pages_between_the_months | HouseholdPlanPageTests.The_household_month_leads_back_to_the_months | Kein Monatsumschalter im Monat (R15, R16) |
| HouseholdPlanPageTests | The_target_household_opens_the_plan_of_the_current_month | HouseholdPlanPageTests.The_target_household_lists_the_months_of_the_household | Haushaltsplan beginnt mit der Liste der Monate (R16) |
| InterfaceBehaviourTests | Switching_back_to_device_uses_the_latest_device_setting | ThemeSwitchTests.A3_without_a_saved_choice_the_device_applies | Ein Knopf ohne Wahl "wie das Gerät"; ohne Wahl gilt das Gerät (R11) |
| InterfaceBehaviourTests | The_member_switch_stands_in_the_top_bar_and_in_the_side_bar | InterfaceBehaviourTests.A13_the_person_choice_sits_in_the_profile_menu_and_not_in_the_top_bar | Personenauswahl im Profil-Menü (R11) |
| InterfaceBehaviourTests | The_page_title_is_the_only_main_heading_and_the_top_bar_title_is_none | InterfaceBehaviourTests.A13_the_page_title_stands_once_and_the_top_bar_has_none | Kein Titel in der oberen Leiste (R11) |
| InterfaceRulesTests | A_long_name_is_shortened_in_the_switch | InterfaceRulesTests.A_long_name_is_shortened_in_the_profile_and_the_top_bar | Umschalter entfällt (R11) |
| InterfaceRulesTests | Below_the_desktop_the_side_bar_is_hidden_and_the_bottom_bar_shown | InterfaceRulesTests.A14_A21_phone_has_the_bottom_bar_and_tablet_the_collapsed_side_bar | Tablet mit eingeklappter Seitenleiste (R19) |
| InterfaceRulesTests | The_ranges_are_phone_to_599_tablet_to_1023_and_desktop_from_1024_without_a_gap | InterfaceRulesTests.The_ranges_are_phone_to_767_tablet_to_1279_and_desktop_from_1280_without_a_gap | Neue Breitenbereiche (R13, R19) |
| MonthPageTests | The_blocks_stand_side_by_side_and_below_each_other_only_on_narrow_screens | MonthPageTests.The_blocks_stand_side_by_side_and_swipe_sideways_on_phones | Kennzahlen am Handy wischbar (R12) |
| MonthPageTests | The_gap_chart_shows_planned_against_actual_in_euro_without_an_alarm_colour | MonthPageTests.The_pot_chart_shows_planned_against_booked_in_euro_without_an_alarm_colour | Rundes Diagramm statt Balken (R14) |
| MonthPageTests | The_three_blocks_show_income_allocatable_and_what_is_still_due | MonthPageTests.The_four_blocks_show_income_allocatable_what_is_still_due_and_the_carry_over | Vier gleich große Kennzahlen (R15) |
| MonthReviewTests | The_review_shows_the_distribution_and_planned_against_booked_as_charts | MonthReviewTests.The_review_shows_a_round_chart_of_the_pots_planned_against_booked, MonthReviewTests.The_review_shows_a_round_chart_of_the_categories_with_the_rest_as_others | Runde Diagramme statt Balken (R14) |
| PlanOverviewPageTests | Paging_between_the_months_keeps_the_address_in_step | PlanOverviewPageTests.The_month_has_no_switch_and_leads_back_to_the_months | Kein Monatsumschalter im Monat (R15); Monatswechsel über die Adresse prüfen PlanPageTests.Paging_shows_the_next_and_the_previous_month und PlanOverviewPageTests.Other_parameters_of_the_open_page_load_the_other_month |
| SavingsPageTests | The_address_sparen_lies_under_more_and_has_its_own_entry_in_the_side_bar | SavingsPageTests.The_address_sparen_has_its_own_entry_in_both_bars | Sparen in der unteren Leiste (R12) |
| SideNavigationTests | The_app_shows_the_three_way_theme_switch_in_the_top_bar | SideNavigationTests.A13_the_app_shows_one_theme_toggle_in_the_top_bar | Ein Knopf für hell und dunkel (R11) |
| SideNavigationTests | The_bottom_bar_has_the_four_targets_of_the_human | SideNavigationTests.A14_the_bottom_bar_has_the_five_targets_of_the_human | Untere Leiste mit Sparen (R12) |
| SideNavigationTests | The_browser_keeps_the_theme_switch_in_the_top_bar_only | SideNavigationTests.A13_the_browser_shows_one_theme_toggle_in_the_top_bar_only | Ein Knopf für hell und dunkel (R11) |
| SideNavigationTests | The_side_bar_has_the_member_switch_on_top_and_the_user_menu_at_the_bottom_even_when_alone | SideNavigationTests.A13_the_side_bar_has_the_profile_menu_at_the_bottom_even_when_alone | Personenauswahl im Profil-Menü (R11) |
| ThemeModeTests | The_switch_has_three_named_buttons_and_marks_the_chosen_one | ThemeModeTests.The_toggle_is_one_named_button_that_switches_to_the_other_mode | Ein Knopf für hell und dunkel (R11) |
| ThemeSwitchTests | A1_choosing_an_option_makes_it_the_only_active_one_and_applies_it | ThemeSwitchTests.A1_the_toggle_switches_to_the_other_mode_and_applies_it | Ein Knopf für hell und dunkel (R11) |
| ThemeSwitchTests | A1_the_switch_offers_light_dark_and_device_and_exactly_one_is_active | ThemeSwitchTests.A1_there_is_exactly_one_named_toggle | Ein Knopf für hell und dunkel (R11) |
| ThemeSwitchTests | A3_without_a_saved_choice_the_device_is_active | ThemeSwitchTests.A3_without_a_saved_choice_the_device_applies | Ein Knopf für hell und dunkel (R11) |

## Nach Toms Durchsicht der Testansicht am 03.11.2026

Drei Tests sind durch Tests mit neuem Namen ersetzt, weil sich ihr Gegenstand verschoben hat. Ohne Ersatz ist keiner entfallen.

| Testklasse | Alter Test | Ersetzt durch | Grund |
| --- | --- | --- | --- |
| MonthPageTests | The_target_actual_bar_of_a_pot_shows_the_actual_percent_as_text | MonthPageTests.The_pot_chart_legend_shows_target_and_actual_of_each_pot_and_there_is_no_sums_box | Ziel und Ist in der Legende des Topf-Diagramms statt eigener Balken (Tom: keine doppelte Darstellung) |
| HouseholdPlanPageTests | The_sums_and_quotas_of_the_household_are_shown | HouseholdPlanPageTests.The_quotas_of_the_household_are_shown_in_the_pot_chart | Textkasten mit Summen entfällt (Tom: "ganz unten immer noch totaler scheiß") |
| HouseholdPlanPageTests | Accounts_are_shown_for_those_who_granted_them_and_the_others_are_named | HouseholdPlanPageTests.Accounts_are_shown_per_person_in_the_review_for_those_who_granted_them_and_the_others_are_named | Kontostände in der Auswertung je Person (Tom: "die kontostände ansich können auch zu auswertung") |

## Sankey entfällt (Tom, 03.11.2026: "eigentlich kanns weg")

Mit dem Flussdiagramm entfallen seine Tests ersatzlos, weil es die geprüfte Sache nicht mehr gibt; ein Test ist umbenannt.

| Testklasse | Alter Test | Ersetzt durch | Grund |
| --- | --- | --- | --- |
| FlowChartSizeTests | A_month_with_ten_positions_is_wide_and_flat_on_the_pc_and_the_phone | entfällt; MonthSizeTests.The_stylesheet_has_no_flow_chart_rules prüft, dass nichts übrig ist | Sankey entfällt |
| FlowChartSizeTests | The_labels_keep_a_readable_size | entfällt | Sankey entfällt |
| FlowChartSizeTests | On_the_phone_each_metric_box_is_only_as_high_as_its_content | MonthSizeTests.On_the_phone_each_metric_box_is_only_as_high_as_its_content | nur verschoben |
| MonthPageTests | The_plan_tab_groups_positions_by_pot_with_amount_tick_box_unplanned_line_and_sankey | MonthPageTests.The_plan_tab_groups_positions_by_pot_with_amount_tick_box_and_unplanned_line | prüft jetzt, dass es keinen Sankey gibt (A10 neu) |
