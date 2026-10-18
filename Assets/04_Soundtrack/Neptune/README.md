# Audio de Neptuno

En la escena, busca `b_NeptuneAudio` en la jerarquía.

- `Music_2D`: arrastra tu música a **Audio Source > Audio Resource**. Ya tiene Loop, Play On Awake y volumen 0,25.
- `AmbientWind_2D`: viento continuo ya conectado al clip existente del proyecto.
- `BlizzardGust_2D`: refuerzo de viento controlado por las ráfagas de `b_NeptuneWeather`.
- En el componente `b_NeptuneAudio` del objeto principal, añade clips a **Ice Cracks** para activar crujidos ocasionales. `IceCracks_3D` los reproduce en distintas posiciones alrededor del jugador.

Guarda los archivos de música en `Music/`, viento y ventiscas en `Ambient/`, y crujidos en `Ice/`. Importar un archivo a estas carpetas no lo asigna automáticamente: arrástralo al campo correspondiente.
