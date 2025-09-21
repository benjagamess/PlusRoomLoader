namespace RewriteRoomLoader
{
    public class RoomJsonData
    {
        public int roomType = 0;
        public int[] floorSpawns = new int[] { 0,1,2,3,4 };
        public bool[] floorTypeSpawns = new bool[] {true,true,true,true};
        public int[] spawnWeights = new int[] { 100,125,150,175,200 };
        public int minItemValue = 0;
        public int maxItemValue = 50;
        public float windowChance = 0f;
        public bool inEndless = true;
    }
}