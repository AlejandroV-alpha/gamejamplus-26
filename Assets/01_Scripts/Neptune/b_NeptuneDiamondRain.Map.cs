using UnityEngine;

public sealed partial class b_NeptuneDiamondRain
{
    ParticleSystem mapRain;
    bool mapRainFilled;
    float mapRainRate, mapRainLifetime, mapRainWarmTime;

    void PrepareMapRain()
    {
        // A fixed world volume keeps distant rain present before the player arrives.
        Terrain terrain = null;
        foreach (var candidate in Terrain.activeTerrains)
            if (candidate.gameObject.scene==gameObject.scene) {terrain=candidate;break;}
        if (terrain==null || terrain.terrainData==null) return;
        var origin = terrain.GetPosition();
        var size = terrain.terrainData.size;
        float top = origin.y+size.y+12;
        foreach (var collider in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (!collider.enabled || collider.isTrigger || collider.gameObject.scene!=gameObject.scene ||
                collider.gameObject.layer!=0 || !(collider is MeshCollider)) continue;
            top = Mathf.Max(top,collider.bounds.max.y+12);
        }
        mapRainLifetime = Mathf.Clamp((top-origin.y)/16+2,4,60);
        const int maximum = 16000;
        mapRainRate = maximum*.8f/mapRainLifetime;
        mapRain = MakeSystem("Diamond Rain - Whole Neptune Map",maximum);
        mapRain.transform.position = new Vector3(origin.x+size.x*.5f,top,origin.z+size.z*.5f);
        var main = mapRain.main;
        main.duration = mapRainLifetime;
        main.startLifetime = mapRainLifetime;
        main.startSize = new ParticleSystem.MinMaxCurve(.07f,.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.65f,.85f,1),new Color(.85f,.2f,.65f));
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var shape = mapRain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size.x+16,1,size.z+16);
        var velocity = mapRain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 2;velocity.y = -16;velocity.z = -.5f;
        var collision = mapRain.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.enableDynamicColliders = false;
        collision.collidesWith = 1;
        collision.lifetimeLoss = 1;
        collision.sendCollisionMessages = false;
        var renderer = mapRain.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = diamondMesh;
        renderer.sharedMaterial = diamondMaterial;
        renderer.enabled = false;
        mapRainFilled = false;
        mapRainWarmTime = 0;
    }

    void UpdateMapRain(float envelope)
    {
        if (mapRain==null) return;
        bool prepareBeforeStorm = !IsRaining && SecondsUntilRain<=mapRainLifetime;
        var renderer = mapRain.GetComponent<ParticleSystemRenderer>();
        renderer.enabled = IsRaining && envelope>.05f && viewCamera!=null;
        if ((!IsRaining && !prepareBeforeStorm) || viewCamera==null)
        {
            if (mapRainFilled) mapRain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            mapRainFilled = false;
            mapRainWarmTime = 0;
            return;
        }
        var emission = mapRain.emission;
        if (!mapRainFilled)
        {
            emission.rateOverTime = mapRainRate;
            mapRain.Play();
            mapRainFilled = true;
        }
        // Normal storms fill invisibly during the forecast. Joining a storm or
        // a very high ice roof can need the remainder simulated immediately.
        if (IsRaining && mapRainWarmTime<mapRainLifetime)
        {
            mapRain.Simulate(mapRainLifetime-mapRainWarmTime,true,false,true);
            mapRain.Play();
            mapRainWarmTime = mapRainLifetime;
        }
        else if (prepareBeforeStorm) mapRainWarmTime = Mathf.Min(mapRainLifetime,mapRainWarmTime+Time.deltaTime);
        emission.rateOverTime = mapRainRate*(prepareBeforeStorm?1:envelope);
    }

    void DisposeMapRain()
    {
        if (mapRain!=null) Remove(mapRain.gameObject);
        mapRain=null;
        mapRainFilled=false;
    }
}
