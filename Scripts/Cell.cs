using UnityEngine;

public class Cell : MonoBehaviour
{
    public bool isObstacle;
    public bool isOccupied;
    public bool isInfoCell;
    public int x;
    public int y;

    public void SetCoordinates(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public void SetObstacle(bool obstacle)
    {
        if (!isInfoCell)
        {
            isObstacle = obstacle;
            GetComponent<SpriteRenderer>().color = obstacle ? Color.gray : Color.white;
        }
    }
}