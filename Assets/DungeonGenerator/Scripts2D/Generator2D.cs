using System.Collections.Generic;
using Graphs;
using UnityEngine;
using Random = System.Random;

public class Generator2D : MonoBehaviour
{
    enum CellType
    {
        None,
        Room,
        Door,
        Hallway
    }

    class Room
    {
        public RectInt bounds;

        public Room(Vector2Int location, Vector2Int size)
        {
            bounds = new RectInt(location, size);
        }

        public static bool Intersect(Room a, Room b)
        {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField]
    Vector2Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector2Int roomMaxSize;
    [SerializeField]
    Vector2Int roomMinSize;
    [SerializeField]
    GameObject cubePrefab;
    [SerializeField]
    Material redMaterial;
    [SerializeField]
    Material blueMaterial;

    [SerializeField]
    int ramdomSeed;

    Random random;
    Grid2D<CellType> grid;
    List<Room> rooms;
    Delaunay2D delaunay;
    HashSet<Prim.Edge> selectedEdges;

    [SerializeField]
    GameObject floorTilePrefab;
    [SerializeField]
    GameObject wallPrefab;
    [SerializeField]
    GameObject doorPrefab;
    [SerializeField]
    GameObject pillarPrefab;
    [SerializeField]
    GameObject lampPrefab;

    void Start()
    {
        Generate();
    }

    void Generate()
    {
        random = new Random(ramdomSeed);
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();
    }

    //void PlaceRooms()
    //{
    //    for (int i = 0; i < roomCount; i++)
    //    {
    //        Vector2Int location = new Vector2Int(
    //            random.Next(0, size.x),
    //            random.Next(0, size.y)
    //        );

    //        Vector2Int roomSize = new Vector2Int(
    //            random.Next(1, roomMaxSize.x + 1),
    //            random.Next(1, roomMaxSize.y + 1)
    //        );

    //        bool add = true;
    //        Room newRoom = new Room(location, roomSize);
    //        Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

    //        foreach (var room in rooms)
    //        {
    //            if (Room.Intersect(room, buffer))
    //            {
    //                add = false;
    //                break;
    //            }
    //        }

    //        if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
    //            || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y)
    //        {
    //            add = false;
    //        }

    //        if (add)
    //        {
    //            rooms.Add(newRoom);
    //            PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

    //            foreach (var pos in newRoom.bounds.allPositionsWithin)
    //            {
    //                grid[pos] = CellType.Room;
    //            }
    //        }
    //    }
    //}

    void PlaceRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            Vector2Int location = new Vector2Int(
                random.Next(0, size.x),
                random.Next(0, size.y)
            );

            Vector2Int roomSize = new Vector2Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            foreach (var room in rooms)
            {
                if (Room.Intersect(room, buffer))
                {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y)
            {
                add = false;
            }

            if (add)
            {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                foreach (var pos in newRoom.bounds.allPositionsWithin)
                {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate()
    {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms)
        {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay2D.Triangulate(vertices);
    }

    void CreateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> mst = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(mst);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges)
        {
            if (random.NextDouble() < 0.125)
            {
                selectedEdges.Add(edge);
            }
        }
    }

    //void PathfindHallways()
    //{
    //    DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);

    //    foreach (var edge in selectedEdges)
    //    {
    //        var startRoom = (edge.U as Vertex<Room>).Item;
    //        var endRoom = (edge.V as Vertex<Room>).Item;

    //        var startPosf = startRoom.bounds.center;
    //        var endPosf = endRoom.bounds.center;
    //        var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
    //        var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

    //        var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) =>
    //        {
    //            var pathCost = new DungeonPathfinder2D.PathCost();

    //            pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

    //            if (grid[b.Position] == CellType.Room)
    //            {
    //                pathCost.cost += 10;
    //            }
    //            else if (grid[b.Position] == CellType.None)
    //            {
    //                pathCost.cost += 5;
    //            }
    //            else if (grid[b.Position] == CellType.Hallway)
    //            {
    //                pathCost.cost += 1;
    //            }

    //            pathCost.traversable = true;

    //            return pathCost;
    //        });

    //        if (path != null)
    //        {
    //            for (int i = 0; i < path.Count; i++)
    //            {
    //                var current = path[i];

    //                if (grid[current] == CellType.None)
    //                {
    //                    grid[current] = CellType.Hallway;
    //                }

    //                if (i > 0)
    //                {
    //                    var prev = path[i - 1];

    //                    var delta = current - prev;
    //                }
    //            }

    //            foreach (var pos in path)
    //            {
    //                if (grid[pos] == CellType.Hallway)
    //                {
    //                    PlaceHallway(pos);
    //                }
    //            }
    //        }
    //    }
    //}

    void PathfindHallways()
    {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);

        foreach (var edge in selectedEdges)
        {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) =>
            {
                var pathCost = new DungeonPathfinder2D.PathCost();

                // Heurística de costo: favorecer pasillos y evitar habitaciones.
                pathCost.cost = Vector2Int.Distance(b.Position, endPos);

                if (grid[b.Position] == CellType.Room)
                {
                    pathCost.cost += 10;
                }
                else if (grid[b.Position] == CellType.None)
                {
                    pathCost.cost += 5;
                }
                else if (grid[b.Position] == CellType.Hallway)
                {
                    pathCost.cost += 1;
                }

                pathCost.traversable = true;

                return pathCost;
            });

            if (path != null)
            {
                foreach (var pos in path)
                {
                    if (grid[pos] == CellType.None)
                    {
                        grid[pos] = CellType.Hallway;
                        PlaceHallway(pos);
                    }
                }
            }
        }
    }

    void TryPlaceDoor(Vector2Int position, Vector2Int direction)
    {
        Vector2Int adjacentPos = position + direction;

        // Solo coloca una puerta si hay una transición válida.
        if (grid[position] == CellType.Room && grid[adjacentPos] == CellType.Hallway)
        {
            Vector3 doorPosition = new Vector3(position.x + direction.x * 0.5f, 0, position.y + direction.y * 0.5f);

            // Determina si la puerta es vertical u horizontal.
            bool isVertical = direction.x != 0;

            // Coloca la puerta
            PlaceDoor(doorPosition, isVertical);

            // Marca en el grid que aquí hay una puerta, opcional
            grid[position] = CellType.Door; // Si decides agregar un tipo de celda "Door"
        }
    }

    void PlaceCube(Vector2Int location, Vector2Int size, Material material)
    {
        GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        go.GetComponent<MeshRenderer>().material = material;
    }

    void PlaceRoom(Vector2Int location, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector3 position = new Vector3(location.x + x, 0, location.y + y);
                Instantiate(floorTilePrefab, position, Quaternion.identity);

                // Colocar paredes y puertas
                if (x == 0)
                {
                    Vector2Int wallPos = new Vector2Int(location.x + x, location.y + y);
                    TryPlaceDoor(wallPos, Vector2Int.left); // Verificar puerta
                    if (grid[wallPos + Vector2Int.left] != CellType.Hallway)
                        PlaceWall(position + Vector3.left, true); // Pared izquierda
                }
                if (x == size.x - 1)
                {
                    Vector2Int wallPos = new Vector2Int(location.x + x, location.y + y);
                    TryPlaceDoor(wallPos, Vector2Int.right); // Verificar puerta
                    if (grid[wallPos + Vector2Int.right] != CellType.Hallway)
                        PlaceWall(position + Vector3.right, true); // Pared derecha
                }
                if (y == 0)
                {
                    Vector2Int wallPos = new Vector2Int(location.x + x, location.y + y);
                    TryPlaceDoor(wallPos, Vector2Int.down); // Verificar puerta
                    if (grid[wallPos + Vector2Int.down] != CellType.Hallway)
                        PlaceWall(position + Vector3.back, false); // Pared trasera
                }
                if (y == size.y - 1)
                {
                    Vector2Int wallPos = new Vector2Int(location.x + x, location.y + y);
                    TryPlaceDoor(wallPos, Vector2Int.up); // Verificar puerta
                    if (grid[wallPos + Vector2Int.up] != CellType.Hallway)
                        PlaceWall(position + Vector3.forward, false); // Pared frontal
                }
            }
        }
    }

    //void PlaceHallway(Vector2Int location)
    //{
    //    Vector3 position = new Vector3(location.x, 0, location.y);
    //    Instantiate(floorTilePrefab, position, Quaternion.identity);

    //    // Opcional: Agregar paredes laterales
    //    if (grid[location.x - 1, location.y] != CellType.Hallway)
    //    {
    //        // Pared izquierda (vertical)
    //        Instantiate(wallPrefab, position + Vector3.left, Quaternion.Euler(0, 90, 0));
    //    }
    //    if (grid[location.x + 1, location.y] != CellType.Hallway)
    //    {
    //        // Pared derecha (vertical)
    //        Instantiate(wallPrefab, position + Vector3.right, Quaternion.Euler(0, 90, 0));
    //    }
    //    if (grid[location.x, location.y - 1] != CellType.Hallway)
    //    {
    //        // Pared trasera (horizontal)
    //        Instantiate(wallPrefab, position + Vector3.back, Quaternion.identity);
    //    }
    //    if (grid[location.x, location.y + 1] != CellType.Hallway)
    //    {
    //        // Pared frontal (horizontal)
    //        Instantiate(wallPrefab, position + Vector3.forward, Quaternion.identity);
    //    }
    //}
    void PlaceHallway(Vector2Int location)
    {
        Vector3 position = new Vector3(location.x, 0, location.y);
        Instantiate(floorTilePrefab, position, Quaternion.identity);

        // Reglas para colocar paredes laterales.
        if (IsWallRequired(location + Vector2Int.left))
        {
            // Pared izquierda (vertical)
            Instantiate(wallPrefab, position + Vector3.left, Quaternion.Euler(0, 90, 0));
        }
        if (IsWallRequired(location + Vector2Int.right))
        {
            // Pared derecha (vertical)
            Instantiate(wallPrefab, position + Vector3.right, Quaternion.Euler(0, 90, 0));
        }
        if (IsWallRequired(location + Vector2Int.down))
        {
            // Pared trasera (horizontal)
            Instantiate(wallPrefab, position + Vector3.back, Quaternion.identity);
        }
        if (IsWallRequired(location + Vector2Int.up))
        {
            // Pared frontal (horizontal)
            Instantiate(wallPrefab, position + Vector3.forward, Quaternion.identity);
        }
    }

    bool IsWallRequired(Vector2Int adjacent)
    {
        // Asegúrate de que las coordenadas estén dentro de los límites del grid.
        if (adjacent.x < 0 || adjacent.x >= size.x || adjacent.y < 0 || adjacent.y >= size.y)
        {
            return false;
        }

        // No colocar paredes si la celda adyacente es parte de un pasillo o una sala.
        if (grid[adjacent] == CellType.Hallway || grid[adjacent] == CellType.Room)
        {
            return false;
        }

        return true; // Se requiere una pared si no es transitable.
    }

    void PlaceWall(Vector3 position, bool isVertical)
    {
        // Si es vertical, rota la pared 90 grados en el eje Y
        Quaternion rotation = isVertical ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;

        // Instancia la pared
        Instantiate(wallPrefab, position, rotation);
    }

    void PlaceDoor(Vector3 position, bool isVertical)
    {
        // Si es una puerta vertical, rotarla 90 grados en el eje Y
        Quaternion rotation = isVertical ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;

        // Instancia la puerta
        Instantiate(doorPrefab, position, rotation);
    }



    void AddDecorations(Vector3 position)
    {
        if (UnityEngine.Random.value > 0.7f)
        { // 30% probabilidad
            Instantiate(pillarPrefab, position + Vector3.up, Quaternion.identity);
        }

        if (UnityEngine.Random.value > 0.9f)
        { // 10% probabilidad
            Instantiate(lampPrefab, position + Vector3.up * 2, Quaternion.identity);
        }
    }


    //void PlaceRoom(Vector2Int location, Vector2Int size)
    //{
    //    PlaceCube(location, size, redMaterial);
    //}

    //void PlaceHallway(Vector2Int location)
    //{
    //    PlaceCube(location, new Vector2Int(1, 1), blueMaterial);
    //}
}
