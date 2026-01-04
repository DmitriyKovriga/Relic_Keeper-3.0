using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class LevelBoundaryGenerator : MonoBehaviour
{
    // Сюда мы скопируем точки, чтобы создать стену
    private EdgeCollider2D _wallCollider;
    private PolygonCollider2D _boundsCollider;

    private void Awake()
    {
        _boundsCollider = GetComponent<PolygonCollider2D>();
        
        // Создаем или находим компонент EdgeCollider2D (это будет наша стена)
        _wallCollider = GetComponent<EdgeCollider2D>();
        if (_wallCollider == null)
        {
            _wallCollider = gameObject.AddComponent<EdgeCollider2D>();
        }

        GenerateWall();
    }

    private void GenerateWall()
    {
        // 1. Берем точки из полигона (границы камеры)
        Vector2[] points = _boundsCollider.points;

        // 2. Edge Collider - это незамкнутая линия. Чтобы стена замкнулась,
        // нужно добавить первую точку еще раз в самый конец списка.
        Vector2[] loopPoints = new Vector2[points.Length + 1];
        System.Array.Copy(points, loopPoints, points.Length);
        loopPoints[points.Length] = points[0]; // Замыкаем круг

        // 3. Применяем точки к стене
        _wallCollider.points = loopPoints;
        
        // 4. Настраиваем физику стены
        _wallCollider.isTrigger = false; // Стена должна быть твердой!
        // _wallCollider.edgeRadius = 0.1f; // Можно добавить толщину, если игрок проскакивает
    }
}