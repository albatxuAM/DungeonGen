using System.Collections.Generic;
using Graphs;
using UnityEngine;
using Random = System.Random;

public class GeneratorCube2D : MonoBehaviour
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
    Material greenMaterial;

    [SerializeField]
    int ramdomSeed;

    Random random;
    Grid2D<CellType> grid;
    List<Room> rooms;
    Delaunay2D delaunay;
    HashSet<Prim.Edge> selectedEdges;

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

        grid.Print();

        PathfindHallways();

        grid.Print();

        //PlaceDoors();
        //grid.Print();
    }

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

                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

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
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];

                    if (grid[current] == CellType.None)
                    {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0)
                    {
                        var prev = path[i - 1];

                        var delta = current - prev;
                    }
                }

                //foreach (var pos in path)
                //{
                //    if (grid[pos] == CellType.Hallway)
                //    {
                //        PlaceHallway(pos);
                //    }
                //}

                for (int i = 0; i < path.Count - 1; i++)
                {
                    var curr = path[i];
                    var next = path[i + 1];

                    if (grid[curr] == CellType.Hallway)
                    {
                        PlaceHallway(curr);
                    }

                    // Verificar si la posición actual es una sala y la siguiente es un pasillo
                    if (grid[curr] == CellType.Room && grid[next] == CellType.Hallway)
                    {
                        // Verificar que la ubicación anterior no es una puerta antes de colocarla
                        // if (grid[next] != CellType.Door)
                        {
                            PlaceDoor(curr);
                            grid[curr] = CellType.Door;  // Asignar 'Door' en la posición donde se coloca la puerta
                        }
                    }
                    else if (grid[next] == CellType.Room && grid[curr] == CellType.Hallway)
                    {
                        // Verificar que la ubicación anterior no es una puerta antes de colocarla
                        // if (grid[next] != CellType.Door)
                        {
                            PlaceDoor(next);
                            grid[next] = CellType.Door;  // Asignar 'Door' en la posición donde se coloca la puerta
                        }
                    }
                }
            }
        }
    }

    private void PlaceDoors()
    {
        // Recorrer el grid completo
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int currentPos = new Vector2Int(x, y);

                // Verificar si la celda actual es un pasillo
                if (grid[currentPos] == CellType.Hallway)
                {
                    // Verificar si algún vecino es una habitación
                    foreach (var neighbor in GetNeighbors(currentPos))
                    {
                        // Si el vecino es una habitación, colocar una puerta en la posición actual
                        if (grid[neighbor] == CellType.Room)
                        {
                            PlaceDoor(currentPos);
                            grid[currentPos] = CellType.Door;
                            break; // Solo colocar una puerta una vez por pasillo
                        }
                    }
                }
            }
        }
    }

    private List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        // Definir las posiciones de los vecinos (arriba, abajo, izquierda, derecha)
        Vector2Int[] directions = new Vector2Int[]
        {
        new Vector2Int(0, 1),  // Arriba
        new Vector2Int(0, -1), // Abajo
        new Vector2Int(1, 0),  // Derecha
        new Vector2Int(-1, 0)  // Izquierda
        };

        // Comprobar los vecinos en las direcciones definidas
        foreach (var direction in directions)
        {
            Vector2Int neighbor = pos + direction;
            if (InBounds(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    private bool InBounds(Vector2Int pos)
    {
        // Verifica si la posición está dentro de los límites del grid
        return pos.x >= 0 && pos.x < size.x && pos.y >= 0 && pos.y < size.y;
    }

    void PlaceCube(Vector2Int location, Vector2Int size, Material material)
    {
        GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        go.GetComponent<MeshRenderer>().material = material;
    }

    void PlaceRoom(Vector2Int location, Vector2Int size)
    {
        PlaceCube(location, size, redMaterial);
    }

    void PlaceHallway(Vector2Int location)
    {
        PlaceCube(location, new Vector2Int(1, 1), blueMaterial);
    }

    void PlaceDoor(Vector2Int location)
    {
        PlaceCube(location, new Vector2Int(1, 1), greenMaterial);
    }
}
