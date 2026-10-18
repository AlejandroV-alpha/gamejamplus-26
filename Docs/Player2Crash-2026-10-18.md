# Crash de Player 2 durante el viaje a Marte

Reporte local: `Crash_2026-10-18_070627570`, proceso virtual 61416,
Unity 6000.5.6f1, DirectX 12. Archivos originales, fuera de Git:
`%LOCALAPPDATA%/Temp/Unity/Editor/Crashes/Crash_2026-10-18_070627570`.
Contiene un minidump y `Fusion213Upgrade.log-mppma9e88e16.txt`.

## Hechos observados

1. Se inició el fundido blanco del portal.
2. La carga de `j_MarsFusion` registró 13097 ms totales, incluidos 10475 ms
   de integración. También apareció un aviso D3D12 sobre un buffer de subida:
   16 MiB disponibles frente a unos 114 MB solicitados.
3. Photon avisó de más de cinco segundos sin `SendOutgoingCommands` y registró
   una desconexión `DisconnectedByPluginLogic`. El log no aporta una razón
   suficiente para atribuir ese rechazo a un timeout concreto del servidor.
4. El gestor de escenas mostró una NullReferenceException en `IsMultiplePeer`,
   con la carga aún finalizándose después del cierre del runner.
5. El proceso terminó con un crash nativo al liberar memoria:
   `UnsafeUtility.FreeTracked` → `CompilerContextData.Cleanup` →
   `RenderGraphCompilationCache.Cleanup` → `RenderGraph.Cleanup` →
   `UniversalRenderPipeline.Dispose` → `RenderPipelineAsset.OnDisable`.
   La parte nativa termina en `UnloadUnusedAssetsOperation`.

Esto localiza el cierre fatal en la limpieza gráfica de Unity/URP. No es el
error anterior de App ID o de `NetworkProjectConfigAsset`. No demuestra por sí
solo la causa de la corrupción de memoria, ni que el video o el fundido la
produzcan. La carga lenta y la desconexión ocurren antes del fallo nativo.

## Siguiente comprobación

Repetir el recorrido completo usando el Editor y un ejecutable Windows de la
misma revisión, en lugar del jugador virtual, manteniendo iguales escena,
configuración gráfica, App IDs y región. Comparar con la misma prueba usando
Player 2. Conservar los logs si reaparece. Solo si el fallo se reproduce se
debe elegir entre una mitigación de Render Graph, otra API gráfica o una
actualización de Unity; no se han cambiado esas opciones a ciegas.

Los informes de crash completos pueden contener datos privados de la máquina
y de las sesiones: no añadirlos al repositorio ni publicarlos sin revisión.
