using Bitgem.VFX.StylisedWater;
using UnityEngine;

// WaterVolumeBox(사각 블록)와 같은 방식이지만, 타일을 사각형이 아니라 원형으로 채운다.
// WaterVolumeBase의 메쉬 생성 로직(월드좌표 UV, 거품용 버텍스 컬러 등)을 그대로 재사용하므로
// Bitgem의 물 쉐이더가 기대하는 데이터를 그대로 가진 "진짜" 원형 물을 만들 수 있다.
[AddComponentMenu("Bitgem/Water Volume (Circle)")]
public class WaterVolumeCircle : WaterVolumeBase
{
    public float Radius = 10f;

    protected override void GenerateTiles(ref bool[,,] _tiles)
    {
        int diameterTiles = Mathf.Clamp(Mathf.RoundToInt((Radius * 2f) / TileSize), 1, MAX_TILES_X);
        float radiusInTiles = Radius / TileSize;
        float centerTile = (diameterTiles - 1) / 2f;

        int maxZ = Mathf.Min(diameterTiles, MAX_TILES_Z);

        for (int x = 0; x < diameterTiles; x++)
        {
            for (int z = 0; z < maxZ; z++)
            {
                float dx = x - centerTile;
                float dz = z - centerTile;
                if (dx * dx + dz * dz <= radiusInTiles * radiusInTiles)
                    _tiles[x, 0, z] = true;
            }
        }
    }

    public override void Validate()
    {
        Radius = Mathf.Max(Radius, TileSize);
    }
}
