using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace RobotVisionSortingTwin
{
    /// <summary>完全由 C# 几何构建的 WPF 三维产线；相机、工件和机械臂共用米制坐标。</summary>
    public sealed partial class TwinScene : UserControl
    {
        private readonly Viewport3D _viewport = new();
        private readonly PerspectiveCamera _camera = new() { FieldOfView = 43 };
        private readonly Model3DGroup _moving = new();
        private readonly Model3DGroup _highlight = new();
        private readonly Dictionary<Model3D, string> _devices = new();
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly MeshGeometry3D Cube = CreateCube();
        private static readonly MeshGeometry3D Round = CreateCylinder();
        private double _yaw = 39;
        private double _pitch = 37;
        private double _distance = 42;
        private Point _dragOrigin;
        private Point _lastMouse;
        private Point3D _target = new(0, 0, -1.2);
        private string _selected = "ROBOT-01";

        public event EventHandler<string>? DeviceSelected;

        public static Point3D RobotBase => new(1, 1.05, -2.7);
        public static Point3D Pickup => new(1, 1.35, 0);

        /// <summary>创建静态厂房、灯光与运动层；静态网格在所有帧间复用。</summary>
        public TwinScene()
        {
            ClipToBounds = true;
            Background = new SolidColorBrush(Color.FromRgb(14, 24, 39));
            Content = _viewport;
            _viewport.Camera = _camera;
            var world = new Model3DGroup();
            world.Children.Add(new AmbientLight(Color.FromRgb(90, 98, 108)));
            world.Children.Add(new DirectionalLight(Color.FromRgb(255, 248, 235), new Vector3D(-2, -5, -3)));
            world.Children.Add(new DirectionalLight(
                Color.FromRgb(116, 167, 203), new Vector3D(3, -2, 2)));
            BuildFactory(world);
            world.Children.Add(_moving);
            world.Children.Add(_highlight);
            _viewport.Children.Add(new ModelVisual3D { Content = world });
            MouseLeftButtonDown += BeginOrbit;
            MouseLeftButtonUp += EndOrbit;
            MouseMove += Orbit;
            MouseWheel += Zoom;
            UpdateCamera();
        }

        /// <summary>返回料仓中心；三维动画和机械臂逆解共享同一目标点。</summary>
        public static Point3D BinPosition(int index)
        {
            return index switch
            {
                0 => new Point3D(-2.1, 1.05, -4),
                1 => new Point3D(1, 1.05, -5.5),
                _ => new Point3D(4.1, 1.05, -4)
            };
        }

        /// <summary>按状态重建少量运动实例；所有实例复用冻结网格与材质。</summary>
        public void Render(SortingSimulation simulation)
        {
            _moving.Children.Clear();
            var tool = ToolPosition(simulation);
            DrawRobot(_moving, tool);
            foreach (var item in simulation.Items)
            {
                var position = new Point3D(item.X, BeltSurface, 0);
                if (item.Stage == TwinItemStage.Held)
                {
                    position = simulation.RobotProgress < 0.12
                        ? new Point3D(Pickup.X, BeltSurface, Pickup.Z)
                        : (simulation.RobotProgress < 0.67 ? tool : BinPosition((int)item.Route))
                            - new Vector3D(0, ProductHeight(item.Shape), 0);
                }

                DrawProduct(_moving, item.Shape, position);
            }

            for (var i = 0; i < 3; i++)
            {
                var bin = BinPosition(i);
                for (var n = 0; n < Math.Min(simulation.Bins[i], 24); n++)
                {
                    var position = new Point3D(
                        bin.X + (n % 3 - 1) * 0.4,
                        0.4 + n / 6 * 0.19,
                        bin.Z + (n / 3 % 2 - 0.5) * 0.5);
                    DrawProduct(_moving, (TwinProductKind)i, position, 0.6);
                }
            }

            for (var i = 0; i < 22; i++)
            {
                var x = -8.4 + (i * 0.77 + simulation.BeltTravel) % 16.8;
                Box(_moving, new Point3D(x, BeltSurface + 0.003, 0), 0.018, 0.004, 1.16, "#394148");
            }

            DrawStackLight(_moving, simulation);
            _highlight.Children.Clear();
            var anchor = DeviceAnchor(_selected);
            Ring(_highlight, new Point3D(anchor.X, 0.039, anchor.Z), 0.9, 0.008, "#37A4BF", true);
        }

        /// <summary>同步列表选择与场景选择；聚焦可由用户单独触发。</summary>
        public void SelectDevice(string id)
        {
            _selected = id;
        }

        /// <summary>整体、俯视及设备近景机位。</summary>
        public void SetView(string view)
        {
            _target = view == "focus" ? DeviceAnchor(_selected) : new Point3D(0, 0, -1.2);
            if (view == "focus" && _selected == "ROBOT-01")
            {
                _target += new Vector3D(0, 1.25, 0);
            }

            _distance = view != "focus" ? 42
                : _selected == "ROBOT-01" ? 14
                : _selected == "BELT-01" ? 26 : 12;
            _pitch = view == "top" ? 83 : 37;
            _yaw = view == "top" ? 0 : 39;
            UpdateCamera();
        }

        /// <summary>按节拍分段插值抓取、抬升、转移、放置与回程，急停时进度保持不变。</summary>
        public static Point3D ToolPosition(SortingSimulation simulation)
        {
            if (simulation.ActiveItem is null)
            {
                return new Point3D(1, 2.6, -0.6);
            }

            var destination = BinPosition((int)simulation.ActiveItem.Route);
            var pickup = new Point3D(Pickup.X,
                BeltSurface + ProductHeight(simulation.ActiveItem.Shape), Pickup.Z);
            Point3D[] waypoints =
            [
                new Point3D(1, 2.6, -0.6),
                pickup,
                new Point3D(Pickup.X, 2.9, Pickup.Z),
                new Point3D(destination.X, 2.9, destination.Z),
                destination,
                new Point3D(destination.X, 2.9, destination.Z),
                new Point3D(1, 2.6, -0.6)
            ];
            double[] times = [0, 0.12, 0.27, 0.52, 0.67, 0.82, 1];
            var progress = simulation.RobotProgress;
            for (var i = 0; i < times.Length - 1; i++)
            {
                if (progress <= times[i + 1])
                {
                    var t = (progress - times[i]) / (times[i + 1] - times[i]);
                    t = t * t * (3 - 2 * t);
                    return waypoints[i] + (waypoints[i + 1] - waypoints[i]) * t;
                }
            }

            return waypoints[^1];
        }

        /// <summary>两段臂长逆解；腕部和吸盘保持垂直，是展示运动学而非厂商控制程序。</summary>
        public static Point3D ElbowPosition(Point3D tool)
        {
            var wrist = tool + new Vector3D(0, 0.38, 0);
            var delta = wrist - RobotBase;
            var horizontal = Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            var distance = Math.Sqrt(horizontal * horizontal + delta.Y * delta.Y);
            const double length = 2.3;
            var angle = Math.Atan2(delta.Y, horizontal)
                + Math.Acos(Math.Clamp(distance / (2 * length), -1, 1));
            var radius = length * Math.Cos(angle);
            var factor = horizontal > 0.0001 ? radius / horizontal : 0;
            return RobotBase + new Vector3D(
                delta.X * factor, length * Math.Sin(angle), delta.Z * factor);
        }

        /// <summary>设备位置用于选中高亮与近景机位。</summary>
        private static Point3D DeviceAnchor(string id)
        {
            return id switch
            {
                "FEED-01" => new Point3D(-7, 1, 0),
                "CAM-01" => new Point3D(-3.2, 1.7, 0),
                "BELT-01" => new Point3D(-0.6, 1, 0),
                "BIN-A" => BinPosition(0),
                "BIN-B" => BinPosition(1),
                "BIN-C" => BinPosition(2),
                "REJECT-01" => new Point3D(8.7, 0.5, 0),
                _ => RobotBase
            };
        }

        /// <summary>建立程序化厂房、输送机、工业相机、料仓与安全围栏。</summary>
        private void BuildFactory(Model3DGroup world)
        {
            var workshop = BuildWorkshop();
            workshop.Freeze();
            world.Children.Add(workshop);
            Register(world, BuildIndustrialConveyor(), "BELT-01");
            Register(world, BuildLoadingTable(), "FEED-01");
            Register(world, BuildVisionStation(), "CAM-01");
            Register(world, BuildRobotStation(), "ROBOT-01");
            for (int index = 0; index < 3; index++)
            {
                Register(world, BuildIndustrialBin(index), $"BIN-{(char)('A' + index)}");
            }

            Register(world, BuildRejectStation(), "REJECT-01");
            var fence = BuildSafetyFence();
            fence.Freeze();
            world.Children.Add(fence);
        }

        /// <summary>将可点选网格绑定到设备标识。</summary>
        private void Register(Model3DGroup world, Model3DGroup group, string device)
        {
            foreach (var model in group.Children)
            {
                _devices[model] = device;
            }

            group.Freeze();
            world.Children.Add(group);
        }

        /// <summary>以逆解关节点构建工业机械臂、关节与真空末端。</summary>
        private static void DrawRobot(Model3DGroup group, Point3D tool)
        {
            var elbow = ElbowPosition(tool);
            var wrist = tool + new Vector3D(0, 0.38, 0);
            var normal = Vector3D.CrossProduct(wrist - RobotBase, new Vector3D(0, 1, 0));
            if (normal.Length < 0.001)
            {
                normal = new Vector3D(0, 0, 1);
            }

            normal.Normalize();
            ArmHousing(group, RobotBase, elbow, 0.3);
            ArmHousing(group, elbow, wrist, 0.235);
            RobotJoint(group, RobotBase, normal, 0.35);
            RobotJoint(group, elbow, normal, 0.29);
            RobotJoint(group, wrist, new Vector3D(0, 1, 0), 0.155);
            var offset = normal * 0.25;
            Beam(group, RobotBase + offset, elbow + offset, 0.055, "#273944");
            Beam(group, elbow + offset, wrist + offset, 0.045, "#273944");
            Beam(group, RobotBase + offset + new Vector3D(0, 0.08, 0),
                elbow + offset + new Vector3D(0, 0.08, 0), 0.022, "#457A8B");
            Beam(group, elbow + offset + new Vector3D(0, 0.08, 0),
                wrist + offset + new Vector3D(0, 0.08, 0), 0.022, "#457A8B");
            CableCurve(group, RobotBase + offset, (RobotBase + (elbow - RobotBase) * 0.5)
                + normal * 0.65 + new Vector3D(0, 0.1, 0), elbow + offset, 0.035);
            CableCurve(group, elbow + offset, (elbow + (wrist - elbow) * 0.5)
                + normal * 0.6 + new Vector3D(0, 0.16, 0), wrist + offset, 0.03);
            Beam(group, wrist, tool + new Vector3D(0, 0.12, 0), 0.073, "#AEBFC7");
            Box(group, tool + new Vector3D(0, 0.12, 0), 0.36, 0.075, 0.23, "#7B909C");
            foreach (double x in new[] { -0.105, 0.105 })
            {
                Cylinder(group, tool + new Vector3D(x, 0.06, 0), 0.037, 0.07, "#CDD5D7");
                Cylinder(group, tool + new Vector3D(x, 0, 0), 0.065, 0.06, "#30454F");
            }

            Beam(group, wrist + new Vector3D(0.08, -0.06, 0),
                tool + new Vector3D(0.12, 0.15, 0), 0.018, "#4896AA");
        }

        /// <summary>工件颜色与目标料仓保持一致；未知件使用琥珀色。</summary>
        private static void DrawProduct(
            Model3DGroup group, TwinProductKind kind, Point3D position, double scale = 1)
        {
            switch (kind)
            {
                case TwinProductKind.Disc:
                    Ring(group, position, 0.27 * scale, 0.19 * scale, "#59B7F2");
                    break;
                case TwinProductKind.Block:
                    Box(group, position + new Vector3D(0, 0.12 * scale, 0),
                        0.62 * scale, 0.24 * scale, 0.4 * scale, "#5FD5B6");
                    break;
                case TwinProductKind.Plate:
                    Box(group, position + new Vector3D(0, 0.06 * scale, 0),
                        0.45 * scale, 0.12 * scale, 0.45 * scale, "#B79AF0");
                    break;
                default:
                    Box(group, position + new Vector3D(0, 0.1 * scale, 0),
                        0.35 * scale, 0.2 * scale, 0.3 * scale, "#E9AC57");
                    break;
            }
        }

        /// <summary>单位网格实例化为指定尺寸的箱体。</summary>
        private static void Box(
            Model3DGroup group, Point3D center, double x, double y, double z, string color)
        {
            var transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(x, y, z));
            transform.Children.Add(new TranslateTransform3D(center.X, center.Y, center.Z));
            group.Children.Add(new GeometryModel3D(Cube, MaterialFor(color))
            {
                Transform = transform
            });
        }

        /// <summary>圆柱的基点在底面中心。</summary>
        private static void Cylinder(
            Model3DGroup group, Point3D bottom, double radius, double height, string color)
        {
            var transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(radius, height, radius));
            transform.Children.Add(new TranslateTransform3D(bottom.X, bottom.Y, bottom.Z));
            group.Children.Add(new GeometryModel3D(Round, MaterialFor(color))
            {
                Transform = transform
            });
        }

        /// <summary>将单位圆柱沿两点连线放置，用于机械臂连杆。</summary>
        private static void Beam(
            Model3DGroup group, Point3D from, Point3D to, double radius, string color)
        {
            var direction = to - from;
            var length = direction.Length;
            direction.Normalize();
            var axis = Vector3D.CrossProduct(new Vector3D(0, 1, 0), direction);
            var angle = Vector3D.AngleBetween(new Vector3D(0, 1, 0), direction);
            if (axis.Length < 0.0001)
            {
                axis = new Vector3D(1, 0, 0);
            }

            var transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(radius, length, radius));
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(axis, angle)));
            transform.Children.Add(new TranslateTransform3D(from.X, from.Y, from.Z));
            group.Children.Add(new GeometryModel3D(Round, MaterialFor(color))
            {
                Transform = transform
            });
        }

        /// <summary>共享已冻结材质，控制场景对象分配量。</summary>
        private static Material MaterialFor(string hex)
        {
            if (!Materials.TryGetValue(hex, out var material))
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var group = new MaterialGroup();
                group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
                bool matte = hex is "#657174" or "#596669" or "#3C6B64"
                    or "#242B30" or "#394148" or "#303A40";
                group.Children.Add(new SpecularMaterial(new SolidColorBrush(
                    matte ? Color.FromRgb(24, 30, 34) : Color.FromRgb(117, 130, 138)), matte ? 8 : 42));
                if (hex is "#F25565" or "#FFC65C" or "#52E2AB" or "#E5F4EF" or "#EDF5F0")
                {
                    group.Children.Add(new EmissiveMaterial(new SolidColorBrush(color) { Opacity = 0.35 }));
                }
                group.Freeze();
                material = group;
                Materials.Add(hex, material);
            }

            return material;
        }

        /// <summary>设备铭牌由系统字体绘制为本地位图，不需要图片或模型资源。</summary>
        private static void Label(Model3DGroup group, string text, Point3D center, double width)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(23, 40, 54)),
                    null, new Rect(0, 0, 400, 72), 6, 6);
                var formatted = new FormattedText(text, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 30, Brushes.White, 1);
                dc.DrawText(formatted, new Point((400 - formatted.Width) / 2, 15));
            }

            var bitmap = new RenderTargetBitmap(400, 72, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            var h = width * 72 / 400;
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection
                {
                    new(center.X - width / 2, center.Y - h / 2, center.Z),
                    new(center.X + width / 2, center.Y - h / 2, center.Z),
                    new(center.X + width / 2, center.Y + h / 2, center.Z),
                    new(center.X - width / 2, center.Y + h / 2, center.Z)
                },
                TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 },
                TextureCoordinates = new PointCollection
                {
                    new(0, 1), new(1, 1), new(1, 0), new(0, 0)
                }
            };
            var material = new EmissiveMaterial(new ImageBrush(bitmap));
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }

        /// <summary>每面独立顶点，保持立方体的硬表面法线。</summary>
        private static MeshGeometry3D CreateCube()
        {
            var mesh = new MeshGeometry3D();
            Point3D[] p =
            [
                new(-0.5, -0.5, -0.5), new(0.5, -0.5, -0.5),
                new(0.5, 0.5, -0.5), new(-0.5, 0.5, -0.5),
                new(-0.5, -0.5, 0.5), new(0.5, -0.5, 0.5),
                new(0.5, 0.5, 0.5), new(-0.5, 0.5, 0.5)
            ];
            int[][] faces =
            [
                [0, 3, 2, 1], [4, 5, 6, 7], [0, 4, 7, 3],
                [1, 2, 6, 5], [3, 7, 6, 2], [0, 1, 5, 4]
            ];
            foreach (var face in faces)
            {
                var start = mesh.Positions.Count;
                foreach (var index in face)
                {
                    mesh.Positions.Add(p[index]);
                }

                foreach (var index in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    mesh.TriangleIndices.Add(start + index);
                }
            }

            mesh.Freeze();
            return mesh;
        }

        /// <summary>圆柱侧面与端盖分离，避免端面被平滑成球面。</summary>
        private static MeshGeometry3D CreateCylinder()
        {
            var mesh = new MeshGeometry3D();
            const int segments = 24;
            for (var i = 0; i < segments; i++)
            {
                var a = i * Math.PI * 2 / segments;
                var b = (i + 1) * Math.PI * 2 / segments;
                var start = mesh.Positions.Count;
                mesh.Positions.Add(new Point3D(Math.Cos(a), 0, Math.Sin(a)));
                mesh.Positions.Add(new Point3D(Math.Cos(a), 1, Math.Sin(a)));
                mesh.Positions.Add(new Point3D(Math.Cos(b), 1, Math.Sin(b)));
                mesh.Positions.Add(new Point3D(Math.Cos(b), 0, Math.Sin(b)));
                foreach (var index in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    mesh.TriangleIndices.Add(start + index);
                }

                start = mesh.Positions.Count;
                mesh.Positions.Add(new Point3D(0, 1, 0));
                mesh.Positions.Add(new Point3D(Math.Cos(b), 1, Math.Sin(b)));
                mesh.Positions.Add(new Point3D(Math.Cos(a), 1, Math.Sin(a)));
                mesh.Positions.Add(new Point3D(0, 0, 0));
                mesh.Positions.Add(new Point3D(Math.Cos(a), 0, Math.Sin(a)));
                mesh.Positions.Add(new Point3D(Math.Cos(b), 0, Math.Sin(b)));
                for (var n = 0; n < 6; n++)
                {
                    mesh.TriangleIndices.Add(start + n);
                }
            }

            mesh.Freeze();
            return mesh;
        }

        /// <summary>依据球面坐标更新自由相机。</summary>
        private void UpdateCamera()
        {
            var yaw = _yaw * Math.PI / 180;
            var pitch = _pitch * Math.PI / 180;
            var offset = new Vector3D(
                _distance * Math.Cos(pitch) * Math.Sin(yaw),
                _distance * Math.Sin(pitch),
                _distance * Math.Cos(pitch) * Math.Cos(yaw));
            _camera.Position = _target + offset;
            _camera.LookDirection = -offset;
            _camera.UpDirection = new Vector3D(0, 1, 0);
        }

        private void BeginOrbit(object sender, MouseButtonEventArgs e)
        {
            _dragOrigin = _lastMouse = e.GetPosition(this);
            CaptureMouse();
        }

        private void Orbit(object sender, MouseEventArgs e)
        {
            if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var point = e.GetPosition(this);
            _yaw -= (point.X - _lastMouse.X) * 0.35;
            _pitch = Math.Clamp(_pitch + (point.Y - _lastMouse.Y) * 0.3, 12, 85);
            _lastMouse = point;
            UpdateCamera();
        }

        private void EndOrbit(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            var point = e.GetPosition(_viewport);
            if ((point - _dragOrigin).Length > 5)
            {
                return;
            }

            VisualTreeHelper.HitTest(_viewport, null, result =>
            {
                if (result is RayMeshGeometry3DHitTestResult hit)
                {
                    if (_devices.TryGetValue(hit.ModelHit, out var id))
                    {
                        _selected = id;
                        DeviceSelected?.Invoke(this, id);
                        return HitTestResultBehavior.Stop;
                    }

                    if (_moving.Children.Contains(hit.ModelHit))
                    {
                        DeviceSelected?.Invoke(this, "ROBOT-01");
                        _selected = "ROBOT-01";
                        return HitTestResultBehavior.Stop;
                    }
                }

                return HitTestResultBehavior.Continue;
            }, new PointHitTestParameters(point));
        }

        private void Zoom(object sender, MouseWheelEventArgs e)
        {
            _distance = Math.Clamp(_distance * (e.Delta > 0 ? 0.9 : 1.1), 7, 55);
            UpdateCamera();
            e.Handled = true;
        }
    }
}
