# Presentación de la tormenta de Marte

`MarsStormAtmosphere`, en Mars Sandstorm dentro de j_MarsFusion, combina niebla global local y partículas alrededor de la cámara principal. Fusion solo replica el ciclo de SandstormSpawner; las partículas no son NetworkObjects.

- Visibilidad completa de tormenta: niebla lineal desde 3 hasta 38 m. Ajustable en Visibility Distance.
- Entrada gradual 2,5 s y salida 4 s; restaura niebla anterior al acabar o desactivar el efecto.
- Polvo rojizo en la región visible de la cámara, con simulación en espacio mundial: no rota pegado a la pantalla.
- Presupuesto máximo 180 partículas; emisión 60/s, vida 1,5–3 s, sin colisiones, luces ni sombras. Reutiliza b_efectoPolvoMarte y su shader suave existente.

Esto evita simular partículas en cada metro del mapa y por cada jugador remoto. El coste esperado es menor que llenar el mapa con densidad equivalente; no se ha medido una cifra de FPS. El principal coste adicional es la transparencia sobre la pantalla. En equipos modestos reducir Particle Budget a 90 antes de aumentar Visibility Distance. La niebla no evita por sí misma que Unity envíe la geometría distante al renderizado.

## Iniciar inmediatamente

En Play y en el cliente con autoridad de la tormenta, seleccionar Mars Sandstorm y usar el menú contextual del componente SandstormSpawner: Iniciar tormenta ahora (Play, autoridad).

Desde código, con una referencia al spawner:

```csharp
storm.TryStartStorm();
```

Devuelve false si no hay sesión válida o este cliente no tiene autoridad. Reinicia el temporizador usando Storm Duration (20 s por defecto). No requiere coroutine. Los demás clientes reciben el estado normal de Fusion.

Prueba local: TryStartStorm devolvió true, niebla alcanzó 38 m, 137 partículas activas dentro del límite de 180, sin errores de consola. Al finalizar el ciclo la distancia original de 1250 m se restauró y el polvo quedó vacío. Falta medir GPU y FPS en los equipos objetivo y probar simultáneamente con amigos.
