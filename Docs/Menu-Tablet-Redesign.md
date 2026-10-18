# Menu de expedicion: rediseño y cierre al conectar

El menu conserva uGUI, TextMeshPro, TabletRoundedPanel y el sistema de idiomas existente. Paneles azul oscuro, bordes de acero discretos y acento ambar como la tablet. Entrada principal ambar y opcion offline secundaria. Tipografia de titulos JetBrains Mono (SDF dinamico, con licencia OFL incluida); lectura e inputs usan la fuente TMP existente a tamaños mayores. Inputs de 24 puntos y acciones principales de 20–22 puntos. La placa lateral de identidad solo aparece en formato horizontal; en ventanas estrechas se usa un formulario vertical. El chasis se ajusta al espacio del Canvas al redimensionar.

El fallo de cierre estaba en MarsFusionSession.SetMenu: con el runner activo se ocultaba solo el panel de pausa y se dejaba visible el panel inicial. ApplySessionMenuState ahora decide ambas raices juntas. Al entrar se desactivan terminal, pausa, fondo, Canvas y GraphicRaycaster. El MonoBehaviour sigue activo para escuchar Escape. Abrir/cerrar pausa pasa por session.SetMenu para sincronizar MenuOpen y no dejar al jugador moviendose debajo del menu.

Se conserva el inicio exclusivo desde lobby y las preferencias de nombre/sala/idioma. Durante conexion no se pueden editar los campos ni iniciar otra conexion. El idioma adicional de ayuda se traduce a ES/EN/PT.

Validacion sin MCP: compilacion C# completa con la respuesta y referencias del proyecto, salida en .local/MenuValidation; codigo de salida 0. Composicion revisada mediante previsualizaciones estaticas fuera de Unity en 1600x1000 y 702x866. Esas previsualizaciones no son capturas del Editor. Falta comprobar en Play el renderizado real, conexion online/offline, Escape, error/reintento y cambio de escena; no se uso MCP porque esta reservado por el equipo.
