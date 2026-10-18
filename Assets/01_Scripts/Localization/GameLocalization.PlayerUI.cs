using System.Collections.Generic;

namespace GameJam.Localization
{
    public static partial class GameLocalization
    {
        private static void AddPlayerTranslations()
        {
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
