
namespace Match3.Gameplay.Board
{
    [System.Serializable]
    public class ArrayLayout
    {

        [System.Serializable]
        public struct rowData
        {
            public bool[] row;
        }

        public rowData[] rows = CreateRows();

        private static rowData[] CreateRows()
        {
            rowData[] result = new rowData[BoardDefinition.TotalHeight];

            for (int y = 0; y < result.Length; y++)
            {
                result[y].row = new bool[BoardDefinition.VisibleWidth];
            }

            return result;
        }

    }
}
