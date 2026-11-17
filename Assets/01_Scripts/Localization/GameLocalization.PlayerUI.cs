using System.Collections.Generic;

namespace GameJam.Localization
{
    public static partial class GameLocalization
    {
        private static void AddPlayerTranslations()
        {
            Add("material.primary_weapon.name", "Arma principal", "Primary weapon", "Arma principal");
            Add("material.primary_weapon.description", "Equípala para apuntar y disparar. Ocupa un espacio.", "Equip to aim and fire. Uses one slot.", "Equipe para mirar e atirar. Ocupa um espaço.");
            Add("material.walkie_talkie.name", "Walkie-talkie", "Walkie-talkie", "Walkie-talkie");
            Add("material.walkie_talkie.description", "Llévalo para recibir radio. Equípalo y haz clic para hablar a distancia.", "Carry to receive radio. Equip and click to talk at any distance.", "Leve para receber rádio. Equipe e clique para falar à distância.");
            Add("radio.channel_open", "Radio · Canal abierto · Clic para cerrar", "Radio · Channel open · Click to close", "Rádio · Canal aberto · Clique para fechar");
            Add("radio.channel_closed", "Radio · Canal cerrado · Clic para hablar", "Radio · Channel closed · Click to talk", "Rádio · Canal fechado · Clique para falar");
            Add("hud.carry_full", "No hay espacio suficiente. Suelta un objeto con G.", "Not enough space. Drop an item with G.", "Sem espaço suficiente. Solte um item com G.");
            Add("hud.carry_empty", "Espacio vacío", "Empty slot", "Espaço vazio");
            Add("hud.carry_help", "{0}/{1} espacios  ·  Números / rueda: seleccionar  ·  G: soltar", "{0}/{1} slots  ·  Numbers / wheel: select  ·  G: drop", "{0}/{1} espaços  ·  Números / roda: selecionar  ·  G: soltar");
            Add("hud.carry_drop_blocked", "No hay espacio seguro para soltarlo aquí.", "No safe space to drop the item here.", "Sem espaço seguro para soltar o item aqui.");
            Add("pause.continue", "Continuar misión", "Continue mission", "Continuar missão");
            Add("pause.return", "Volver al lobby", "Return to lobby", "Voltar ao lobby");
            Add("pause.disconnect", "Desconectar", "Disconnect", "Desconectar");
            Add("pause.mission.lobby", "Zona de espera", "Staging area", "Área de espera");
            Add("pause.mission.active", "Misión en curso", "Mission in progress", "Missão em andamento");
            Add("pause.mission.repaired", "Nave reparada", "Ship repaired", "Nave reparada");
            Add("pause.mission.progress", "{0}/3 fabricadas · {1}/3 instaladas", "{0}/3 crafted · {1}/3 installed", "{0}/3 fabricadas · {1}/3 instaladas");
            Add("pause.mission.waiting", "La expedición aún no ha comenzado", "The expedition has yet to begin", "A expedição ainda não começou");
            Add("pause.mission.sync", "Sin datos de reparación disponibles", "Repair data unavailable", "Dados de reparo indisponíveis");
            Add("crew.muted", "En espera", "Standby", "Em espera");
            Add("crew.speaking", "Hablando", "Speaking", "Falando");
            Add("crew.silence", "Silenciar", "Mute", "Silenciar");
            Add("crew.unmute", "Escuchar", "Unmute", "Ouvir");
            Add("pause.new_title", "Menú de misión", "Mission menu", "Menu da missão");
            Add("pause.new_subtitle", "La partida sigue activa mientras consultas el menú.", "The session stays active while you use this menu.", "A partida continua enquanto você usa este menu.");
            Add("pause.your_mission", "De vuelta a la expedición", "Back to the expedition", "De volta à expedição");
            Add("pause.controls_location", "Controles y teclas en Ajustes", "Controls and bindings in Settings", "Controles e teclas em Ajustes");

            Add("tablet.mission_ui_0", "CATALOGO · Sin misión activa en este planeta", "CATALOG · No active mission on this planet", "CATÁLOGO · Nenhuma missão ativa neste planeta");
            Add("tablet.mission_ui_1", "INSTALANDO ", "INSTALLING ", "INSTALANDO ");
            Add("tablet.mission_ui_3", "/3 instaladas", "/3 installed", "/3 instaladas");
            Add("tablet.mission_ui_4", "NAVE REPARADA · 3/3 componentes instalados", "SHIP REPAIRED · 3/3 components installed", "NAVE REPARADA · 3/3 componentes instalados");
            Add("tablet.mission_ui_5", "/3 fabricadas · ", "/3 crafted · ", "/3 fabricadas · ");
            Add("tablet.mission_ui_6", "RECETA DE REFERENCIA", "REFERENCE RECIPE", "RECEITA DE REFERÊNCIA");
            Add("tablet.mission_ui_7", "[x] INSTALADO", "[x] INSTALLED", "[x] INSTALADO");
            Add("tablet.mission_ui_8", "FABRICADO · Instalar con Q", "CRAFTED · Install with Q", "FABRICADO · Instalar com Q");
            Add("tablet.mission_ui_9", "PENDIENTE DE FABRICAR", "NOT CRAFTED YET", "AGUARDANDO FABRICAÇÃO");
            Add("tablet.mission_ui_10", "Checklist · ", "Checklist · ", "Lista de tarefas · ");
            Add("tablet.mission_ui_11", "[x] FABRICADO", "[x] CRAFTED", "[x] FABRICADO");
            Add("tablet.mission_ui_12", "[ ] FABRICAR", "[ ] CRAFT", "[ ] FABRICAR");
            Add("tablet.mission_ui_13", "[ ] INSTALAR", "[ ] INSTALL", "[ ] INSTALAR");
            Add("tablet.mission_ui_14", "Progreso compartido del equipo · Fabricar en caja [E] · Instalar en nave [Q]", "Shared team progress · Craft at station [E] · Install on ship [Q]", "Progresso da equipe · Fabricar na bancada [E] · Instalar na nave [Q]");
            Add("tablet.mission_ui_15", "Recetas · ", "Recipes · ", "Receitas · ");
            Add("tablet.mission_ui_16", " · Cantidades de TU inventario", " · Quantities in YOUR inventory", " · Quantidades no SEU inventário");
            Add("tablet.mission_ui_17", "Ingredientes > Componente · Usa la caja de crafteo [E] para fabricar; la tablet muestra el estado.", "Ingredients > Component · Craft at the station [E]; the tablet tracks progress.", "Ingredientes > Componente · Fabrique na bancada [E]; a tablet mostra o progresso.");
            Add("tablet.mission_ui_18", "Registro de misión", "Mission log", "Registro da missão");
            Add("tablet.mission_ui_19", "Sin actividad registrada en esta misión.", "No activity recorded in this mission.", "Nenhuma atividade registrada nesta missão.");
            Add("tablet.mission_ui_20", "Recogidas, fabricación e instalaciones · Actualización automática desde el estado de red.", "Pickups, crafting and installations · Automatically synced with the session.", "Coletas, fabricação e instalações · Sincronizado automaticamente com a sessão.");
            Add("tablet.mission_ui_21", "\n[TAB] Ver progreso de misión", "\n[TAB] View mission progress", "\n[TAB] Ver progresso da missão");
            Add("console.leave", "SALIR DEL\nLOBBY", "LEAVE\nLOBBY", "SAIR DO\nLOBBY");
            Add("console.active", "SECUENCIA\nACTIVA", "SEQUENCE\nACTIVE", "SEQUÊNCIA\nATIVA");
            Add("console.cancel", "CANCELAR\nLISTO", "CANCEL\nREADY", "CANCELAR\nPRONTO");
            Add("console.ready", "ESTOY\nLISTO", "I AM\nREADY", "ESTOU\nPRONTO");
            Add("console.leaving", "SALIENDO...", "LEAVING...", "SAINDO...");
            Add("console.waiting", "ESPERANDO JUGADOR", "WAITING FOR PLAYER", "AGUARDANDO JOGADOR");
            Add("console.cancelled", "LISTO CANCELADO", "READY CANCELLED", "PRONTO CANCELADO");
            Add("craft.open", "[E] Abrir mesa de fabricación", "[E] Open crafting station", "[E] Abrir bancada de fabricação");
            Add("craft.title", "MESA DE FABRICACIÓN", "CRAFTING STATION", "BANCADA DE FABRICAÇÃO");
            Add("craft.help", "Junta materiales y fabrica las piezas de la nave", "Gather materials and craft ship parts", "Junte materiais e fabrique peças da nave");
            Add("craft.close", "[E] o [Esc] para cerrar", "[E] or [Esc] to close", "[E] ou [Esc] para fechar");
            Add("craft.done", "Fabricado", "Crafted", "Fabricado");
            Add("craft.make", "Fabricar", "Craft", "Fabricar");
            Add("craft.missing", "Faltan materiales", "Missing materials", "Faltam materiais");
            Add("craft.busy", "Fabricando...", "Crafting...", "Fabricando...");
            Add("ending.return", "Volver al menú", "Return to menu", "Voltar ao menu");
            Add("ending.intro", "<size=32><cspace=8>EL FINAL DE UNA MISIÓN</cspace></size>\n\n<size=86><b>UNA HISTORIA\nHECHA EN EQUIPO</b></size>", "<size=32><cspace=8>THE END OF A MISSION</cspace></size>\n\n<size=86><b>A STORY\nMADE TOGETHER</b></size>", "<size=32><cspace=8>O FIM DE UMA MISSÃO</cspace></size>\n\n<size=86><b>UMA HISTÓRIA\nFEITA EM EQUIPE</b></size>");
            Add("ending.thanks", "<size=76><b>GRACIAS POR JUGAR</b></size>\n\n<size=28><cspace=5>HASTA LA PRÓXIMA MISIÓN</cspace></size>", "<size=76><b>THANKS FOR PLAYING</b></size>\n\n<size=28><cspace=5>SEE YOU NEXT MISSION</cspace></size>", "<size=76><b>OBRIGADO POR JOGAR</b></size>\n\n<size=28><cspace=5>ATÉ A PRÓXIMA MISSÃO</cspace></size>");
            Add("session.ready", "Photon Fusion — listo", "Photon Fusion — ready", "Photon Fusion — pronto");
            Add("session.local_start", "Iniciando prueba local...", "Starting local session...", "Iniciando sessão local...");
            Add("session.connecting", "Conectando con Photon...", "Connecting to Photon...", "Conectando ao Photon...");
            Add("session.connected", "Conectado — ", "Connected — ", "Conectado — ");
            Add("session.closed", "Sesión cerrada: ", "Session closed: ", "Sessão encerrada: ");
            Add("session.failed", "No se pudo iniciar: ", "Could not start: ", "Não foi possível iniciar: ");
            Add("lobby.crew", "TRIPULACIÓN", "CREW", "TRIPULAÇÃO");
            Add("lobby.connecting", "CONECTANDO...", "CONNECTING...", "CONECTANDO...");
            Add("lobby.free", "PLAZA LIBRE", "EMPTY SLOT", "VAGA LIVRE");
            Add("lobby.ready", "LISTO", "READY", "PRONTO");
            Add("lobby.waiting", "ESPERANDO", "WAITING", "AGUARDANDO");
            Add("hud.health", "VIDA", "HEALTH", "VIDA");
            Add("hud.stamina", "ESTAMINA", "STAMINA", "VIGOR");
            Add("hud.cold", "FRÍO", "COLD", "FRIO");
            Add("hud.heat", "CALOR", "HEAT", "CALOR");
            Add("hud.environment", "AMBIENTE", "ENVIRONMENT", "AMBIENTE");
            Add("hud.no_sensor", "Sin sensor", "No sensor", "Sem sensor");
            Add("hud.tablet", "TABLET", "DATAPAD", "TABLET");
            Add("hud.equipped", "EQUIPADO", "EQUIPPED", "EQUIPADO");
            Add("hud.weapon", "Arma", "Weapon", "Arma");
            Add("hud.empty_hands", "Manos libres", "Empty hands", "Mãos livres");
            Add("hud.inventory", "Inventario", "Inventory", "Inventário");
            Add("hud.close", "Cerrar", "Close", "Fechar");
            Add("hud.empty", "Aún no tienes materiales.", "No materials yet.", "Você ainda não tem materiais.");
            Add("hud.select", "Selecciona un material", "Select a material", "Selecione um material");
            Add("hud.quantity", "Cantidad: {0}", "Quantity: {0}", "Quantidade: {0}");
            Add("hud.pickup", "+{0} {1}", "+{0} {1}", "+{0} {1}");
            Add("hud.collecting", "Recogiendo…", "Collecting…", "Coletando…");
            Add("hud.repairing", "Reparando…", "Repairing…", "Reparando…");
            Add("hud.material", "Material de fabricación", "Crafting material", "Material de fabricação");
            Add("hud.repair_ship", "Reparar nave", "Repair ship", "Reparar nave");
            Add("hud.return_to_zone", "REGRESA A LA ZONA EXPLORABLE", "RETURN TO THE EXPLORATION ZONE", "VOLTE À ZONA DE EXPLORAÇÃO");
            Add("hud.return_before_timeout", "Vuelve antes de que termine el contador", "Return before the countdown ends", "Volte antes que a contagem termine");
            Add("material.red_mineral.name", "Mineral rojo", "Red mineral", "Minério vermelho");
            Add("material.metal.name", "Metal", "Metal", "Metal");
            Add("material.ice_crystal.name", "Cristal de hielo", "Ice crystal", "Cristal de gelo");
            Add("material.energy_crystal.name", "Cristal energético", "Energy crystal", "Cristal energético");
            Add("material.circuit.name", "Circuito", "Circuit", "Circuito");
            Add("material.damaged_intake.name", "Toma de aire dañada", "Damaged air intake", "Entrada de ar danificada");
            Add("material.damaged_panel.name", "Panel de casco dañado", "Damaged hull panel", "Painel do casco danificado");
            Add("material.damaged_propulsion.name", "Turbina dañada", "Damaged turbine", "Turbina danificada");
            Add("material.red_mineral.description", "Mineral utilizado en componentes de Marte.", "Mineral used in Mars components.", "Minério usado em componentes de Marte.");
            Add("material.metal.description", "Material metálico para componentes y estructuras.", "Metal for components and structures.", "Metal para componentes e estruturas.");
            Add("material.ice_crystal.description", "Cristal utilizado en componentes de Neptuno.", "Crystal used in Neptune components.", "Cristal usado em componentes de Netuno.");
            Add("material.energy_crystal.description", "Cristal utilizado en componentes de Saturno.", "Crystal used in Saturn components.", "Cristal usado em componentes de Saturno.");
            Add("material.circuit.description", "Componente electrónico para sistemas de control.", "Electronic component for control systems.", "Componente eletrônico para sistemas de controle.");
            foreach (string id in new[] { "damaged_intake", "damaged_panel", "damaged_propulsion" })
                Add("material." + id + ".description", "Pieza recuperada de la nave. Combínala con minerales para reparar.", "Recovered ship part. Combine with minerals to repair.", "Peça recuperada da nave. Combine com minérios para reparar.");
        }

        private static void Add(string key, string es, string en, string pt)
        {
            Translations.Add(key, new Dictionary<GameLanguage, string>
            {
                [GameLanguage.Spanish] = es,
                [GameLanguage.English] = en,
                [GameLanguage.Portuguese] = pt
            });
        }
    }
}
