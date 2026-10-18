using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Localization
{
    public enum GameLanguage
    {
        Spanish = 0,
        English = 1,
        Portuguese = 2
    }

    /// <summary>
    /// Sistema centralizado de traducción y localización para Español, Inglés y Portugués (Brasil).
    /// Soporta cambio de idioma en caliente y persistencia en PlayerPrefs.
    /// </summary>
    public static partial class GameLocalization
    {
        static GameLocalization() => AddPlayerTranslations();
        private const string PrefKey = "GameJam.Language";
        public static event Action OnLanguageChanged;

        private static GameLanguage currentLanguage = GameLanguage.Spanish;
        private static bool initialized = false;

        public static GameLanguage CurrentLanguage
        {
            get
            {
                EnsureInitialized();
                return currentLanguage;
            }
            set
            {
                if (currentLanguage == value && initialized) return;
                currentLanguage = value;
                initialized = true;
                PlayerPrefs.SetInt(PrefKey, (int)currentLanguage);
                PlayerPrefs.Save();
                OnLanguageChanged?.Invoke();
            }
        }

        public static string CurrentLanguageCode => CurrentLanguage switch
        {
            GameLanguage.English => "EN",
            GameLanguage.Portuguese => "PT-BR",
            _ => "ES"
        };

        public static string CurrentLanguageDisplayName => CurrentLanguage switch
        {
            GameLanguage.English => "English",
            GameLanguage.Portuguese => "Português (Brasil)",
            _ => "Español"
        };

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            if (PlayerPrefs.HasKey(PrefKey))
            {
                currentLanguage = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2);
            }
            else
            {
                // Detectar idioma del sistema
                currentLanguage = Application.systemLanguage switch
                {
                    SystemLanguage.Portuguese => GameLanguage.Portuguese,
                    SystemLanguage.English => GameLanguage.English,
                    _ => GameLanguage.Spanish
                };
                PlayerPrefs.SetInt(PrefKey, (int)currentLanguage);
            }
        }

        public static string Get(string key, string fallback = null)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;

            if (Translations.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(currentLanguage, out var text) && !string.IsNullOrEmpty(text))
                    return text;
                // Fallback a Español o Inglés
                if (dict.TryGetValue(GameLanguage.Spanish, out var esText) && !string.IsNullOrEmpty(esText))
                    return esText;
                if (dict.TryGetValue(GameLanguage.English, out var enText) && !string.IsNullOrEmpty(enText))
                    return enText;
            }

            return fallback ?? key;
        }

        public static string Tr(this string key, string fallback = null) => Get(key, fallback);

        // Diccionario completo de traducciones
        private static readonly Dictionary<string, Dictionary<GameLanguage, string>> Translations = new()
        {
            // === SELECTOR DE IDIOMA ===
            ["lang.title"] = new() {
                [GameLanguage.Spanish] = "SELECCIONA TU IDIOMA",
                [GameLanguage.English] = "SELECT YOUR LANGUAGE",
                [GameLanguage.Portuguese] = "SELECIONE SEU IDIOMA"
            },
            ["lang.es"] = new() {
                [GameLanguage.Spanish] = "Español",
                [GameLanguage.English] = "Spanish",
                [GameLanguage.Portuguese] = "Espanhol"
            },
            ["lang.en"] = new() {
                [GameLanguage.Spanish] = "Inglés",
                [GameLanguage.English] = "English",
                [GameLanguage.Portuguese] = "Inglês"
            },
            ["lang.pt"] = new() {
                [GameLanguage.Spanish] = "Portugués (Brasil)",
                [GameLanguage.English] = "Portuguese (Brazil)",
                [GameLanguage.Portuguese] = "Português (Brasil)"
            },

            // === MENÚ DE INICIO / CONEXIÓN DE SALA ===
            ["menu.connection_hint"] = new() {
                [GameLanguage.Spanish] = "Comparte el mismo código de sala con tu tripulación.",
                [GameLanguage.English] = "Share the same room code with your crew.",
                [GameLanguage.Portuguese] = "Compartilhe o mesmo código de sala com sua tripulação."
            },
            ["menu.main_title"] = new() {
                [GameLanguage.Spanish] = "TERMINAL DE EXPEDICIÓN",
                [GameLanguage.English] = "EXPEDITION TERMINAL",
                [GameLanguage.Portuguese] = "TERMINAL DE EXPEDIÇÃO"
            },
            ["menu.subtitle"] = new() {
                [GameLanguage.Spanish] = "SISTEMA DE MISIONES Y CONEXIÓN",
                [GameLanguage.English] = "MISSION & CONNECTION SYSTEM",
                [GameLanguage.Portuguese] = "SISTEMA DE MISSÕES E CONEXÃO"
            },
            ["menu.player_name"] = new() {
                [GameLanguage.Spanish] = "NOMBRE DEL ASTRONAUTA",
                [GameLanguage.English] = "ASTRONAUT NAME",
                [GameLanguage.Portuguese] = "NOME DO ASTRONAUTA"
            },
            ["menu.player_name_placeholder"] = new() {
                [GameLanguage.Spanish] = "Ingresa tu nombre...",
                [GameLanguage.English] = "Enter your name...",
                [GameLanguage.Portuguese] = "Digite seu nome..."
            },
            ["menu.room_code"] = new() {
                [GameLanguage.Spanish] = "CÓDIGO DE LA SALA / MISIÓN",
                [GameLanguage.English] = "ROOM / MISSION CODE",
                [GameLanguage.Portuguese] = "CÓDIGO DA SALA / MISSÃO"
            },
            ["menu.room_code_placeholder"] = new() {
                [GameLanguage.Spanish] = "Ej: MARTE-01",
                [GameLanguage.English] = "Ex: MARS-01",
                [GameLanguage.Portuguese] = "Ex: MARTE-01"
            },
            ["menu.btn_join_online"] = new() {
                [GameLanguage.Spanish] = "CREAR / UNIRSE A SALA ONLINE",
                [GameLanguage.English] = "CREATE / JOIN ONLINE ROOM",
                [GameLanguage.Portuguese] = "CRIAR / ENTRAR NA SALA ONLINE"
            },
            ["menu.btn_solo"] = new() {
                [GameLanguage.Spanish] = "MISIÓN EN SOLITARIO (OFFLINE)",
                [GameLanguage.English] = "SOLO MISSION (OFFLINE)",
                [GameLanguage.Portuguese] = "MISSÃO SOLO (OFFLINE)"
            },
            ["menu.btn_settings"] = new() {
                [GameLanguage.Spanish] = "AJUSTES / CONFIGURACIÓN",
                [GameLanguage.English] = "SETTINGS & CONFIG",
                [GameLanguage.Portuguese] = "CONFIGURAÇÕES"
            },
            ["menu.status_ready"] = new() {
                [GameLanguage.Spanish] = "Photon Fusion — Listo para conectar",
                [GameLanguage.English] = "Photon Fusion — Ready to connect",
                [GameLanguage.Portuguese] = "Photon Fusion — Pronto para conectar"
            },
            ["menu.status_connecting"] = new() {
                [GameLanguage.Spanish] = "Conectando con Photon...",
                [GameLanguage.English] = "Connecting to Photon...",
                [GameLanguage.Portuguese] = "Conectando ao Photon..."
            },
            ["menu.status_local"] = new() {
                [GameLanguage.Spanish] = "Iniciando misión local...",
                [GameLanguage.English] = "Starting local mission...",
                [GameLanguage.Portuguese] = "Iniciando missão local..."
            },
            ["menu.status_connected"] = new() {
                [GameLanguage.Spanish] = "Conectado a la sala",
                [GameLanguage.English] = "Connected to room",
                [GameLanguage.Portuguese] = "Conectado à sala"
            },

            // === MENÚ DE PAUSA ===
            ["pause.title"] = new() {
                [GameLanguage.Spanish] = "SISTEMA EN PAUSA",
                [GameLanguage.English] = "SYSTEM PAUSED",
                [GameLanguage.Portuguese] = "SISTEMA EM PAUSA"
            },
            ["pause.tab_mission"] = new() {
                [GameLanguage.Spanish] = "MISIÓN",
                [GameLanguage.English] = "MISSION",
                [GameLanguage.Portuguese] = "MISSÃO"
            },
            ["pause.tab_settings"] = new() {
                [GameLanguage.Spanish] = "AJUSTES",
                [GameLanguage.English] = "SETTINGS",
                [GameLanguage.Portuguese] = "AJUSTES"
            },
            ["pause.tab_crew"] = new() {
                [GameLanguage.Spanish] = "TRIPULACIÓN",
                [GameLanguage.English] = "CREW",
                [GameLanguage.Portuguese] = "TRIPULAÇÃO"
            },
            ["pause.btn_resume"] = new() {
                [GameLanguage.Spanish] = "CONTINUAR MISIÓN",
                [GameLanguage.English] = "RESUME MISSION",
                [GameLanguage.Portuguese] = "CONTINUAR MISSÃO"
            },
            ["pause.btn_return_lobby"] = new() {
                [GameLanguage.Spanish] = "VOLVER AL LOBBY",
                [GameLanguage.English] = "RETURN TO LOBBY",
                [GameLanguage.Portuguese] = "VOLTAR AO LOBBY"
            },
            ["pause.btn_disconnect"] = new() {
                [GameLanguage.Spanish] = "DESCONECTAR / SALIR",
                [GameLanguage.English] = "DISCONNECT / LEAVE",
                [GameLanguage.Portuguese] = "DESCONECTAR / SAIR"
            },

            // === CONFIGURACIÓN DE AUDIO & VOZ ===
            ["audio.master_volume"] = new() {
                [GameLanguage.Spanish] = "VOLUMEN GENERAL",
                [GameLanguage.English] = "MASTER VOLUME",
                [GameLanguage.Portuguese] = "VOLUME GERAL"
            },
            ["voice.title"] = new() {
                [GameLanguage.Spanish] = "CHAT DE VOZ POR PROXIMIDAD",
                [GameLanguage.English] = "PROXIMITY VOICE CHAT",
                [GameLanguage.Portuguese] = "CHAT DE VOZ POR PROXIMIDADE"
            },
            ["voice.mode"] = new() {
                [GameLanguage.Spanish] = "MODO DE TRANSMISIÓN",
                [GameLanguage.English] = "TRANSMISSION MODE",
                [GameLanguage.Portuguese] = "MODO DE TRANSMISSÃO"
            },
            ["voice.ptt"] = new() {
                [GameLanguage.Spanish] = "Pulsar para Hablar",
                [GameLanguage.English] = "Push to Talk",
                [GameLanguage.Portuguese] = "Pressionar para Falar"
            },
            ["voice.open_mic"] = new() {
                [GameLanguage.Spanish] = "Micrófono Abierto",
                [GameLanguage.English] = "Open Mic",
                [GameLanguage.Portuguese] = "Microfone Aberto"
            },
            ["voice.ptt_key"] = new() {
                [GameLanguage.Spanish] = "Tecla de Hablar: [V]",
                [GameLanguage.English] = "Talk Key: [V]",
                [GameLanguage.Portuguese] = "Tecla para Falar: [V]"
            },
            ["voice.distance"] = new() {
                [GameLanguage.Spanish] = "Alcance de Voz: 22 metros",
                [GameLanguage.English] = "Voice Range: 22 meters",
                [GameLanguage.Portuguese] = "Alcance de Voz: 22 metros"
            },

            // === LISTA DE JUGADORES / TRIPULACIÓN ===
            ["crew.title"] = new() {
                [GameLanguage.Spanish] = "ASTRONAUTAS EN LA SALA",
                [GameLanguage.English] = "ASTRONAUTS IN ROOM",
                [GameLanguage.Portuguese] = "ASTRONAUTAS NA SALA"
            },
            ["crew.you"] = new() {
                [GameLanguage.Spanish] = "(Tú)",
                [GameLanguage.English] = "(You)",
                [GameLanguage.Portuguese] = "(Você)"
            },
            ["crew.voice_volume"] = new() {
                [GameLanguage.Spanish] = "Volumen de Voz",
                [GameLanguage.English] = "Voice Volume",
                [GameLanguage.Portuguese] = "Volume de Voz"
            },
            ["crew.no_other_players"] = new() {
                [GameLanguage.Spanish] = "No hay otros astronautas conectados en esta sala.",
                [GameLanguage.English] = "No other astronauts connected in this room.",
                [GameLanguage.Portuguese] = "Nenhum outro astronauta conectado nesta sala."
            },

            // === CONTROLES / AYUDA ===
            ["controls.title"] = new() {
                [GameLanguage.Spanish] = "GUÍA DE CONTROLES",
                [GameLanguage.English] = "CONTROLS GUIDE",
                [GameLanguage.Portuguese] = "GUIA DE CONTROLES"
            },
            ["controls.move"] = new() {
                [GameLanguage.Spanish] = "WASD: Desplazamiento",
                [GameLanguage.English] = "WASD: Movement",
                [GameLanguage.Portuguese] = "WASD: Movimento"
            },
            ["controls.jump"] = new() {
                [GameLanguage.Spanish] = "Espacio: Saltar · Shift: Correr",
                [GameLanguage.English] = "Space: Jump · Shift: Sprint",
                [GameLanguage.Portuguese] = "Espaço: Pular · Shift: Correr"
            },
            ["controls.tablet"] = new() {
                [GameLanguage.Spanish] = "Tab: Abrir / Cerrar Tablet",
                [GameLanguage.English] = "Tab: Open / Close Tablet",
                [GameLanguage.Portuguese] = "Tab: Abrir / Fechar Tablet"
            },
            ["controls.voice"] = new() {
                [GameLanguage.Spanish] = "V: Hablar (Chat de Proximidad)",
                [GameLanguage.English] = "V: Talk (Proximity Chat)",
                [GameLanguage.Portuguese] = "V: Falar (Chat de Proximidade)"
            },
            ["controls.pause"] = new() {
                [GameLanguage.Spanish] = "Esc: Pausa / Configuración",
                [GameLanguage.English] = "Esc: Pause / Settings",
                [GameLanguage.Portuguese] = "Esc: Pausa / Configurações"
            },

            // === TABLET Y SECCIONES DE MISIÓN ===
            ["tablet.log"] = new() {
                [GameLanguage.Spanish] = "BITÁCORA",
                [GameLanguage.English] = "MISSION LOG",
                [GameLanguage.Portuguese] = "DIÁRIO DE BORDO"
            },
            ["tablet.checklist"] = new() {
                [GameLanguage.Spanish] = "CHECKLIST",
                [GameLanguage.English] = "CHECKLIST",
                [GameLanguage.Portuguese] = "CHECKLIST"
            },
            ["tablet.recipes"] = new() {
                [GameLanguage.Spanish] = "RECETAS",
                [GameLanguage.English] = "RECIPES",
                [GameLanguage.Portuguese] = "RECEITAS"
            },
            ["tablet.hint"] = new() {
                [GameLanguage.Spanish] = "[TAB] TABLET",
                [GameLanguage.English] = "[TAB] DATAPAD",
                [GameLanguage.Portuguese] = "[TAB] TABLET"
            },
            ["tablet.close"] = new() {
                [GameLanguage.Spanish] = "CERRAR",
                [GameLanguage.English] = "CLOSE",
                [GameLanguage.Portuguese] = "FECHAR"
            },
            ["tablet.title"] = new() {
                [GameLanguage.Spanish] = "Tu expedición, a mano.",
                [GameLanguage.English] = "Your expedition, in hand.",
                [GameLanguage.Portuguese] = "Sua expedição, na mão."
            },
            ["tablet.tools"] = new() {
                [GameLanguage.Spanish] = "Herramientas de exploración",
                [GameLanguage.English] = "Exploration Tools",
                [GameLanguage.Portuguese] = "Ferramentas de exploração"
            },
            ["tablet.mission"] = new() {
                [GameLanguage.Spanish] = "MISIÓN",
                [GameLanguage.English] = "MISSION",
                [GameLanguage.Portuguese] = "MISSÃO"
            },
            ["tablet.apps_prompt"] = new() {
                [GameLanguage.Spanish] = "Selecciona una app para abrirla",
                [GameLanguage.English] = "Select an app to open it",
                [GameLanguage.Portuguese] = "Selecione um app para abrir"
            },
            ["tablet.step1"] = new() {
                [GameLanguage.Spanish] = "Reunir los materiales de {0}",
                [GameLanguage.English] = "Gather {0} materials",
                [GameLanguage.Portuguese] = "Coletar os materiais de {0}"
            },
            ["tablet.step2"] = new() {
                [GameLanguage.Spanish] = "Consultar las recetas y reparar los componentes",
                [GameLanguage.English] = "Consult recipes and repair components",
                [GameLanguage.Portuguese] = "Consultar as receitas e reparar os componentes"
            },
            ["tablet.step3"] = new() {
                [GameLanguage.Spanish] = "Preparar la nave para continuar el viaje",
                [GameLanguage.English] = "Prepare the ship to continue the journey",
                [GameLanguage.Portuguese] = "Preparar a nave para continuar a viagem"
            },
            ["tablet.mission_guide"] = new() {
                [GameLanguage.Spanish] = "Guía de misión",
                [GameLanguage.English] = "Mission Guide",
                [GameLanguage.Portuguese] = "Guia de missão"
            },
            ["tablet.repair_guide"] = new() {
                [GameLanguage.Spanish] = "Guía de reparación / Materiales necesarios por componente.",
                [GameLanguage.English] = "Repair guide / Required materials per component.",
                [GameLanguage.Portuguese] = "Guia de reparação / Materiais necessários por componente."
            },
            ["tablet.equipped"] = new() {
                [GameLanguage.Spanish] = "01  TABLET EQUIPADA  [TAB]",
                [GameLanguage.English] = "01  DATAPAD EQUIPPED  [TAB]",
                [GameLanguage.Portuguese] = "01  TABLET EQUIPADO  [TAB]"
            },
            ["tablet.status_bar"] = new() {
                [GameLanguage.Spanish] = "ORBIT / GUÍA DE CAMPO",
                [GameLanguage.English] = "ORBIT / FIELD GUIDE",
                [GameLanguage.Portuguese] = "ORBIT / GUIA DE CAMPO"
            },
            ["tablet.local_archive"] = new() {
                [GameLanguage.Spanish] = "ARCHIVO LOCAL",
                [GameLanguage.English] = "LOCAL ARCHIVE",
                [GameLanguage.Portuguese] = "ARQUIVO LOCAL"
            },
            ["tablet.no_recipes"] = new() {
                [GameLanguage.Spanish] = "No hay recetas registradas para este planeta.",
                [GameLanguage.English] = "No recipes recorded for this planet.",
                [GameLanguage.Portuguese] = "Nenhuma receita registrada para este planeta."
            },
            ["tablet.recipes_subtitle"] = new() {
                [GameLanguage.Spanish] = "Componentes y materiales",
                [GameLanguage.English] = "Components & Materials",
                [GameLanguage.Portuguese] = "Componentes e materiais"
            }
        };
    }
}
