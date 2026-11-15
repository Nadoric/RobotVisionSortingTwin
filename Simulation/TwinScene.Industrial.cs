using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RobotVisionSortingTwin
{
    public sealed partial class TwinScene
    {
        private const double BeltSurface = 1.1;
        private static readonly MeshGeometry3D Sleeve = CreateAnnulus(0.41);
        private static readonly MeshGeometry3D SelectionRing = CreateAnnulus(0.94);
        private static readonly MeshGeometry3D LinkShell = CreateLinkShell();

        private static double ProductHeight(TwinProductKind kind)
        {
            return kind switch
            {
                TwinProductKind.Disc => 0.19,
                TwinProductKind.Block => 0.24,
                TwinProductKind.Plate => 0.12,
                _ => 0.2
            };
        }

        /// <summary>环氧地坪、检修通道与设备落地阴影，厂房构件只创建一次。</summary>
        private static Model3DGroup BuildWorkshop()
        {
            var group = new Model3DGroup();
            Box(group, new Point3D(0, -0.32, -1), 22, 0.6, 15, "#303B43");
            Box(group, new Point3D(0, -0.005, -1), 21.6, 0.04, 14.6, "#657174");
            for (int x = -10; x <= 10; x += 2)
            {
                Box(group, new Point3D(x, 0.019, -1), 0.009, 0.003, 14.4, "#596669");
            }

            for (int z = -8; z <= 6; z += 2)
            {
                Box(group, new Point3D(0, 0.019, z), 21.4, 0.003, 0.009, "#596669");
            }

            Box(group, new Point3D(0, 0.027, 3.55), 20.2, 0.012, 1.6, "#3C6B64");
            foreach (double z in new[] { 2.7, 4.4 })
            {
                Box(group, new Point3D(0, 0.037, z), 20.4, 0.01, 0.065, "#E2BB53");
            }

            for (int x = -9; x <= 9; x += 3)
            {
                Box(group, new Point3D(x, 0.042, 3.55), 0.65, 0.01, 0.07, "#D6E4D9");
                var arrow = new Model3DGroup();
                Box(arrow, new Point3D(x + 0.24, 0.043, 3.43), 0.07, 0.01, 0.3, "#D6E4D9");
                arrow.Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -35),
                    new Point3D(x + 0.24, 0.043, 3.55));
                group.Children.Add(arrow);
            }

            Box(group, new Point3D(0, 1.45, -7.95), 21, 2.9, 0.15, "#899398");
            for (int x = -10; x <= 10; x += 5)
            {
                Box(group, new Point3D(x, 1.9, -7.8), 0.21, 3.8, 0.27, "#4F606A");
                Box(group, new Point3D(x, 0.16, -7.7), 0.45, 0.24, 0.45, "#9EA8AC");
            }

            for (int x = -9; x <= 9; x++)
            {
                Box(group, new Point3D(x, 1.45, -7.86), 0.012, 2.85, 0.015, "#7A878D");
            }

            Box(group, new Point3D(0, 3.78, -7.8), 21, 0.18, 0.3, "#53636C");
            foreach (double x in new[] { -6.5, 0, 6.5 })
            {
                Box(group, new Point3D(x, 3.25, -7.62), 2.5, 0.16, 0.22, "#D4DDE0");
                Box(group, new Point3D(x, 3.2, -7.47), 2.25, 0.065, 0.035, "#EDF5F0");
            }

            Label(group, "VISION SORTING / CELL 01", new Point3D(-6.6, 2.66, -7.66), 4.8);
            Label(group, "AUTOMATED ROBOT CELL", new Point3D(1.5, 2.66, -7.66), 4.2);
            ContactShadow(group, new Point3D(-0.3, 0.03, 0), 18, 2.5);
            ContactShadow(group, new Point3D(1, 0.031, -2.7), 2.5, 2.5);
            ContactShadow(group, new Point3D(7.4, 0.031, -3), 1.7, 1.8);
            return group;
        }

        /// <summary>铝型材支架、端部滚筒、减速电机及护栏组成的皮带输送机。</summary>
        private static Model3DGroup BuildIndustrialConveyor()
        {
            var group = new Model3DGroup();
            Box(group, new Point3D(-0.25, 0.92, 0), 17.0, 0.3, 1.5, "#AFBABF");
            Box(group, new Point3D(-0.25, BeltSurface - 0.018, 0), 16.85, 0.036, 1.22, "#242B30");
            foreach (double z in new[] { -0.76, 0.76 })
            {
                Box(group, new Point3D(-0.25, 0.93, z), 17.1, 0.19, 0.07, "#D0D8DA");
                Box(group, new Point3D(-0.25, 0.925, z + Math.Sign(z) * 0.04), 16.95, 0.035, 0.018, "#56636D");
                Box(group, new Point3D(-0.25, 1.2, z), 17.1, 0.045, 0.045, "#E0E6E6");
            }

            for (int x = -8; x <= 8; x += 2)
            {
                foreach (double z in new[] { -0.61, 0.61 })
                {
                    ProfilePost(group, new Point3D(x, 0.48, z), 0.78);
                    Cylinder(group, new Point3D(x, 0.035, z), 0.14, 0.075, "#303A40");
                    Cylinder(group, new Point3D(x, 0.11, z), 0.045, 0.07, "#BEC8CC");
                    Box(group, new Point3D(x, 1.12, z * 1.25), 0.055, 0.22, 0.045, "#84969F");
                    Beam(group, new Point3D(x, 0.35, z), new Point3D(x + 0.45, 0.8, z), 0.028, "#74858F");
                    Beam(group, new Point3D(x, 0.92, z * 1.28), new Point3D(x, 0.92, z * 1.36), 0.028, "#4B5961");
                }

                Box(group, new Point3D(x, 0.28, 0), 0.075, 0.09, 1.28, "#96A5AD");
            }

            foreach (double x in new[] { -8.7, 8.2 })
            {
                Beam(group, new Point3D(x, 0.96, -0.66), new Point3D(x, 0.96, 0.66), 0.14, "#ADBCC3");
                Beam(group, new Point3D(x, 0.96, -0.84), new Point3D(x, 0.96, 0.84), 0.045, "#D4DDE0");
                Box(group, new Point3D(x, 0.9, 0.84), 0.22, 0.2, 0.12, "#657A88");
            }

            Box(group, new Point3D(-7.85, 0.73, 1.03), 0.52, 0.38, 0.38, "#728A96");
            Beam(group, new Point3D(-7.85, 0.73, 1.08), new Point3D(-7.85, 0.73, 1.85), 0.22, "#415F72");
            for (int index = 0; index < 7; index++)
            {
                Beam(group, new Point3D(-7.85, 0.73, 1.22 + index * 0.08),
                    new Point3D(-7.85, 0.73, 1.25 + index * 0.08), 0.245, "#56798B");
            }

            Beam(group, new Point3D(-7.85, 0.73, 1.86), new Point3D(-7.85, 0.73, 1.98), 0.22, "#2D414F");
            Box(group, new Point3D(-7.45, 0.65, 1.1), 0.32, 0.07, 0.42, "#C5CED1");
            foreach (double x in new[] { -4.1, 0.3, 7.3 })
            {
                Box(group, new Point3D(x, 1.16, 0.85), 0.18, 0.22, 0.13, "#374A56");
                Box(group, new Point3D(x, 1.19, 0.773), 0.09, 0.07, 0.018, "#DC7A45");
                Beam(group, new Point3D(x, 1.06, 0.9), new Point3D(x - 0.15, 0.63, 0.88), 0.018, "#27373F");
            }

            Label(group, "CV-01 / BELT CONVEYOR", new Point3D(-1.1, 0.93, 0.81), 1.7);
            return group;
        }

        private static void ProfilePost(Model3DGroup group, Point3D center, double height)
        {
            Box(group, center, 0.1, height, 0.1, "#C3CCD0");
            Box(group, center + new Vector3D(0.052, 0, 0), 0.008, height * 0.96, 0.032, "#586A74");
            Box(group, center + new Vector3D(0, 0, 0.052), 0.032, height * 0.96, 0.008, "#586A74");
        }

        /// <summary>带工业相机、镜头、条形照明、线槽与接线盒的检测龙门架。</summary>
        private static Model3DGroup BuildVisionStation()
        {
            var group = new Model3DGroup();
            foreach (double z in new[] { -1.15, 1.15 })
            {
                ProfilePost(group, new Point3D(-3.2, 1.5, z), 2.9);
                Box(group, new Point3D(-3.2, 0.08, z), 0.52, 0.12, 0.44, "#9CAEB7");
                foreach (double x in new[] { -3.36, -3.04 })
                {
                    Cylinder(group, new Point3D(x, 0.145, z), 0.035, 0.025, "#435560");
                }
            }

            Box(group, new Point3D(-3.2, 2.96, 0), 0.15, 0.15, 2.48, "#BECBD1");
            Box(group, new Point3D(-3.2, 2.96, 0.079), 0.045, 0.045, 2.4, "#536875");
            Box(group, new Point3D(-3.2, 2.74, 0), 0.4, 0.29, 0.39, "#516877");
            Box(group, new Point3D(-3.2, 2.88, 0), 0.34, 0.025, 0.33, "#A0B2BB");
            for (int index = 0; index < 5; index++)
            {
                Box(group, new Point3D(-3.38, 2.65 + index * 0.04, 0), 0.025, 0.012, 0.3, "#2F4555");
            }

            Cylinder(group, new Point3D(-3.2, 2.39, 0), 0.145, 0.2, "#2C343A");
            Cylinder(group, new Point3D(-3.2, 2.37, 0), 0.16, 0.04, "#5F6A72");
            Cylinder(group, new Point3D(-3.2, 2.365, 0), 0.105, 0.012, "#417F92");
            foreach (double x in new[] { -3.66, -2.74 })
            {
                Box(group, new Point3D(x, 2.43, 0), 0.16, 0.12, 1.42, "#364B59");
                Box(group, new Point3D(x, 2.36, 0), 0.11, 0.025, 1.24, "#E5F4EF");
                Beam(group, new Point3D(x, 2.49, -0.56), new Point3D(-3.2, 2.94, -0.56), 0.025, "#A1B4BE");
            }

            Box(group, new Point3D(-3.2, 1.95, -1.34), 0.42, 0.55, 0.2, "#BFCBCD");
            Box(group, new Point3D(-3.2, 1.96, -1.225), 0.32, 0.42, 0.025, "#D8E0E1");
            Beam(group, new Point3D(-3.2, 2.91, -0.2), new Point3D(-3.2, 3.12, -0.45), 0.025, "#243640");
            Beam(group, new Point3D(-3.2, 3.12, -0.45), new Point3D(-3.2, 3.12, -1.17), 0.025, "#243640");
            Beam(group, new Point3D(-3.2, 3.12, -1.17), new Point3D(-3.2, 2.2, -1.32), 0.025, "#243640");
            Label(group, "CAM-01 / VISION", new Point3D(-3.2, 3.2, 0.18), 1.8);
            return group;
        }

        private static Model3DGroup BuildLoadingTable()
        {
            var group = new Model3DGroup();
            Box(group, new Point3D(-8.6, 0.74, -1.7), 1.75, 1.25, 1.35, "#9AADB7");
            Box(group, new Point3D(-8.6, 1.39, -1.7), 1.92, 0.11, 1.52, "#D1D9D8");
            Box(group, new Point3D(-8.6, 0.74, -1.0), 1.5, 0.97, 0.035, "#BDC9CB");
            Box(group, new Point3D(-8.3, 0.84, -0.97), 0.24, 0.045, 0.04, "#3F5361");
            foreach (double x in new[] { -9.3, -7.9 })
            {
                foreach (double z in new[] { -2.22, -1.18 })
                {
                    Cylinder(group, new Point3D(x, 0.02, z), 0.1, 0.12, "#344752");
                }
            }

            Box(group, new Point3D(-8.6, 1.47, -1.7), 1.7, 0.05, 1.25, "#344854");
            for (int index = 0; index < 6; index++)
            {
                DrawProduct(group, (TwinProductKind)(index % 3),
                    new Point3D(-9.12 + index % 3 * 0.51, 1.5, -1.98 + index / 3 * 0.55), 0.65);
            }

            Label(group, "FEED-01 / LOADING", new Point3D(-8.6, 0.55, -0.95), 1.35);
            return group;
        }

        private static Model3DGroup BuildRobotStation()
        {
            var group = new Model3DGroup();
            Box(group, new Point3D(1, 0.13, -2.7), 1.65, 0.24, 1.65, "#566875");
            Box(group, new Point3D(1, 0.39, -2.7), 1.23, 0.32, 1.23, "#ADB8BE");
            Cylinder(group, new Point3D(1, 0.55, -2.7), 0.52, 0.12, "#384A57");
            Cylinder(group, new Point3D(1, 0.67, -2.7), 0.42, 0.25, "#DB7835");
            Cylinder(group, new Point3D(1, 0.92, -2.7), 0.35, 0.12, "#35434C");
            for (int index = 0; index < 8; index++)
            {
                double angle = index * Math.PI / 4;
                Cylinder(group, new Point3D(1 + Math.Cos(angle) * 0.43, 0.67,
                    -2.7 + Math.Sin(angle) * 0.43), 0.035, 0.025, "#C5CDD0");
            }

            Label(group, "R-01 / ROBOT", new Point3D(1, 0.38, -2.06), 1.05);
            Box(group, new Point3D(7.45, 0.12, -3.1), 1.22, 0.23, 1.05, "#3C4D59");
            Box(group, new Point3D(7.45, 1.0, -3.1), 1.16, 1.55, 0.94, "#C7D0D3");
            Box(group, new Point3D(7.45, 1.0, -2.616), 1.05, 1.43, 0.028, "#DBE1DF");
            Box(group, new Point3D(7.45, 1.36, -2.59), 0.77, 0.46, 0.045, "#354B59");
            Box(group, new Point3D(7.45, 1.36, -2.561), 0.65, 0.34, 0.015, "#469DA9");
            Box(group, new Point3D(7.33, 1.4, -2.55), 0.35, 0.018, 0.012, "#BCDCD7");
            Box(group, new Point3D(7.3, 1.31, -2.55), 0.28, 0.025, 0.012, "#B9D8D3");
            Box(group, new Point3D(7.78, 0.78, -2.58), 0.035, 0.23, 0.06, "#475E6C");
            for (int index = 0; index < 6; index++)
            {
                Box(group, new Point3D(7.46, 0.41 + index * 0.046, -2.58), 0.69, 0.015, 0.018, "#7A8B94");
            }

            Beam(group, new Point3D(7.16, 0.94, -2.57), new Point3D(7.16, 0.94, -2.5), 0.085, "#D7B844");
            Beam(group, new Point3D(7.16, 0.94, -2.5), new Point3D(7.16, 0.94, -2.46), 0.052, "#D44D46");
            Beam(group, new Point3D(7.43, 0.94, -2.57), new Point3D(7.43, 0.94, -2.5), 0.04, "#5EB899");
            Cylinder(group, new Point3D(7.45, 1.8, -3.1), 0.035, 0.36, "#71838E");
            Box(group, new Point3D(4.4, 0.055, -2.7), 4.7, 0.09, 0.24, "#465D6C");
            Box(group, new Point3D(4.4, 0.108, -2.7), 4.65, 0.02, 0.27, "#87969C");
            Label(group, "CONTROL / HMI", new Point3D(7.45, 1.68, -2.57), 0.9);
            return group;
        }

        /// <summary>带加强筋、箱沿、脚轮与仓位标记的周转料箱。</summary>
        private static Model3DGroup BuildIndustrialBin(int index)
        {
            var group = new Model3DGroup();
            var p = BinPosition(index);
            string[] colors = ["#4C9FD3", "#4CB598", "#9C82C7"];
            string[] innerColors = ["#376F91", "#337F6A", "#715E90"];
            ContactShadow(group, new Point3D(p.X, 0.032, p.Z), 2.2, 1.95);
            Box(group, new Point3D(p.X, 0.23, p.Z), 1.9, 0.15, 1.65, innerColors[index]);
            Box(group, new Point3D(p.X, 0.69, p.Z - 0.77), 1.9, 0.86, 0.1, colors[index]);
            Box(group, new Point3D(p.X, 0.54, p.Z + 0.77), 1.9, 0.56, 0.1, colors[index]);
            foreach (double x in new[] { -0.9, 0.9 })
            {
                Box(group, new Point3D(p.X + x, 0.69, p.Z), 0.1, 0.86, 1.6, colors[index]);
                Box(group, new Point3D(p.X + x, 1.13, p.Z), 0.14, 0.065, 1.72, innerColors[index]);
                foreach (double z in new[] { -0.63, 0.63 })
                {
                    Beam(group, new Point3D(p.X + x - 0.045, 0.1, p.Z + z),
                        new Point3D(p.X + x + 0.045, 0.1, p.Z + z), 0.09, "#34424D");
                }
            }

            for (int rib = 0; rib < 5; rib++)
            {
                Box(group, new Point3D(p.X - 0.73 + rib * 0.36, 0.53, p.Z + 0.83),
                    0.045, 0.47, 0.04, innerColors[index]);
            }

            Box(group, new Point3D(p.X, 1.13, p.Z - 0.78), 1.98, 0.065, 0.14, innerColors[index]);
            Box(group, new Point3D(p.X, 0.83, p.Z + 0.79), 1.94, 0.065, 0.13, innerColors[index]);
            Label(group, $"BIN {(char)('A' + index)} / 0{index + 1}", new Point3D(p.X, 0.58, p.Z + 0.86), 1.2);
            return group;
        }

        private static Model3DGroup BuildRejectStation()
        {
            var group = new Model3DGroup();
            Box(group, new Point3D(8.85, 0.32, 0), 1.5, 0.48, 1.55, "#6B7270");
            Box(group, new Point3D(8.85, 0.57, 0), 1.55, 0.12, 1.6, "#B29562");
            Box(group, new Point3D(8.85, 0.69, -0.74), 1.55, 0.35, 0.08, "#C6A361");
            foreach (double x in new[] { 8.1, 9.6 })
            {
                Box(group, new Point3D(x, 0.69, 0), 0.08, 0.35, 1.55, "#C6A361");
            }

            Box(group, new Point3D(8.85, 0.67, 0.75), 1.55, 0.12, 0.1, "#C6A361");
            Label(group, "RECHECK / NG", new Point3D(8.85, 0.4, 0.82), 1.23);
            return group;
        }

        /// <summary>后侧及两侧网格式围护；保留前侧设备展示视线，门框带铰链、门锁和警示牌。</summary>
        private static Model3DGroup BuildSafetyFence()
        {
            var group = new Model3DGroup();
            for (int panel = 0; panel < 6; panel++)
            {
                FencePanel(group, new Point3D(-4.7 + panel * 1.9, 0, -6.8),
                    new Point3D(-4.7 + (panel + 1) * 1.9, 0, -6.8));
            }

            foreach (double x in new[] { -4.7, 6.7 })
            {
                for (int panel = 0; panel < 3; panel++)
                {
                    FencePanel(group, new Point3D(x, 0, -6.8 + panel * 1.55),
                        new Point3D(x, 0, -6.8 + (panel + 1) * 1.55));
                }
            }

            Box(group, new Point3D(6.76, 1.1, -2.85), 0.07, 0.33, 0.11, "#445C68");
            Box(group, new Point3D(6.83, 1.1, -2.85), 0.065, 0.055, 0.23, "#CBD4D4");
            foreach (double y in new[] { 0.42, 1.72 })
            {
                Cylinder(group, new Point3D(6.74, y, -3.68), 0.045, 0.15, "#778790");
            }

            Label(group, "CAUTION / ROBOT", new Point3D(4.8, 1.32, -6.72), 1.5);
            return group;
        }

        private static void FencePanel(Model3DGroup group, Point3D start, Point3D end)
        {
            Vector3D direction = end - start;
            foreach (var position in new[] { start, end })
            {
                Box(group, position + new Vector3D(0, 1.08, 0), 0.075, 2.1, 0.075, "#D5A841");
                Box(group, position + new Vector3D(0, 0.035, 0), 0.24, 0.06, 0.24, "#52626A");
            }

            foreach (double height in new[] { 0.27, 2.08 })
            {
                Beam(group, start + new Vector3D(0, height, 0), end + new Vector3D(0, height, 0), 0.025, "#D5A841");
            }

            for (int index = 1; index < 11; index++)
            {
                var position = start + direction * (index / 11.0);
                Beam(group, position + new Vector3D(0, 0.28, 0), position + new Vector3D(0, 2.05, 0),
                    0.008, "#5D6C72");
            }

            foreach (double height in new[] { 0.65, 1.05, 1.45, 1.85 })
            {
                Beam(group, start + new Vector3D(0, height, 0), end + new Vector3D(0, height, 0), 0.007, "#5D6C72");
            }
        }

        private static void DrawStackLight(Model3DGroup group, SortingSimulation simulation)
        {
            string[] colors =
            [
                simulation.EmergencyStopped ? "#F25565" : "#53383D",
                !simulation.EmergencyStopped && simulation.Alarm.Length > 0 ? "#FFC65C" : "#66583D",
                !simulation.EmergencyStopped && simulation.Alarm.Length == 0 ? "#52E2AB" : "#32534B"
            ];
            for (int index = 0; index < 3; index++)
            {
                Cylinder(group, new Point3D(7.45, 2.16 + index * 0.13, -3.1), 0.085, 0.11, colors[index]);
                Cylinder(group, new Point3D(7.45, 2.27 + index * 0.13, -3.1), 0.088, 0.025, "#334550");
            }

            Cylinder(group, new Point3D(7.45, 2.55, -3.1), 0.087, 0.035, "#334550");
        }

        private static void Ring(Model3DGroup group, Point3D bottom, double radius,
            double height, string color, bool thin = false)
        {
            var transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(radius, height, radius));
            transform.Children.Add(new TranslateTransform3D(bottom.X, bottom.Y, bottom.Z));
            group.Children.Add(new GeometryModel3D(thin ? SelectionRing : Sleeve, MaterialFor(color))
            {
                Transform = transform
            });
        }

        private static void ContactShadow(Model3DGroup group, Point3D center, double width, double depth)
        {
            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(76, 13, 24, 31), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(28, 13, 24, 31), 0.65));
            brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            brush.Freeze();
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection
                {
                    center + new Vector3D(-width / 2, 0, -depth / 2),
                    center + new Vector3D(-width / 2, 0, depth / 2),
                    center + new Vector3D(width / 2, 0, depth / 2),
                    center + new Vector3D(width / 2, 0, -depth / 2)
                },
                TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 },
                TextureCoordinates = new PointCollection { new(0, 0), new(0, 1), new(1, 1), new(1, 0) }
            };
            mesh.Freeze();
            group.Children.Add(new GeometryModel3D(mesh, new DiffuseMaterial(brush)));
        }

        /// <summary>空心轴套包含内壁和上下环面，避免用深色贴片模拟孔洞。</summary>
        private static MeshGeometry3D CreateAnnulus(double innerRadius)
        {
            var mesh = new MeshGeometry3D();
            void Face(Point3D a, Point3D b, Point3D c, Point3D d)
            {
                int first = mesh.Positions.Count;
                mesh.Positions.Add(a);
                mesh.Positions.Add(b);
                mesh.Positions.Add(c);
                mesh.Positions.Add(d);
                foreach (int index in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    mesh.TriangleIndices.Add(first + index);
                }
            }

            for (int index = 0; index < 32; index++)
            {
                double a = index * Math.PI / 16;
                double b = (index + 1) * Math.PI / 16;
                var outerA = new Point3D(Math.Cos(a), 0, Math.Sin(a));
                var outerB = new Point3D(Math.Cos(b), 0, Math.Sin(b));
                var innerA = new Point3D(Math.Cos(a) * innerRadius, 0, Math.Sin(a) * innerRadius);
                var innerB = new Point3D(Math.Cos(b) * innerRadius, 0, Math.Sin(b) * innerRadius);
                var up = new Vector3D(0, 1, 0);
                Face(outerA, outerA + up, outerB + up, outerB);
                Face(innerB, innerB + up, innerA + up, innerA);
                Face(outerA + up, innerA + up, innerB + up, outerB + up);
                Face(outerB, innerB, innerA, outerA);
            }

            mesh.Freeze();
            return mesh;
        }

        private static MeshGeometry3D CreateLinkShell()
        {
            var mesh = new MeshGeometry3D();
            const int sides = 12;
            for (int index = 0; index < sides; index++)
            {
                double a = index * Math.PI * 2 / sides;
                double b = (index + 1) * Math.PI * 2 / sides;
                int start = mesh.Positions.Count;
                mesh.Positions.Add(new Point3D(Math.Cos(a), 0, Math.Sin(a) * 0.8));
                mesh.Positions.Add(new Point3D(Math.Cos(a) * 0.67, 1, Math.Sin(a) * 0.58));
                mesh.Positions.Add(new Point3D(Math.Cos(b) * 0.67, 1, Math.Sin(b) * 0.58));
                mesh.Positions.Add(new Point3D(Math.Cos(b), 0, Math.Sin(b) * 0.8));
                foreach (int triangle in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    mesh.TriangleIndices.Add(start + triangle);
                }
            }

            mesh.Freeze();
            return mesh;
        }

        private static void ArmHousing(Model3DGroup group, Point3D from, Point3D to, double width)
        {
            var direction = to - from;
            double length = direction.Length;
            direction.Normalize();
            var axis = Vector3D.CrossProduct(new Vector3D(0, 1, 0), direction);
            if (axis.Length < 0.0001)
            {
                axis = new Vector3D(1, 0, 0);
            }

            var transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(width, length, width));
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(axis,
                Vector3D.AngleBetween(new Vector3D(0, 1, 0), direction))));
            transform.Children.Add(new TranslateTransform3D(from.X, from.Y, from.Z));
            group.Children.Add(new GeometryModel3D(LinkShell, MaterialFor("#E88135")) { Transform = transform });
        }

        private static void RobotJoint(Model3DGroup group, Point3D center, Vector3D axis, double radius)
        {
            axis.Normalize();
            Beam(group, center - axis * 0.2, center + axis * 0.2, radius, "#334651");
            Beam(group, center - axis * 0.235, center - axis * 0.2, radius * 0.83, "#B7C3C8");
            Beam(group, center + axis * 0.2, center + axis * 0.24, radius * 0.78, "#D3DBDD");
            Beam(group, center + axis * 0.24, center + axis * 0.27, radius * 0.42, "#506573");
            var tangent = Vector3D.CrossProduct(axis, new Vector3D(0, 1, 0));
            if (tangent.Length < 0.01)
            {
                tangent = new Vector3D(1, 0, 0);
            }

            tangent.Normalize();
            var bitangent = Vector3D.CrossProduct(axis, tangent);
            for (int index = 0; index < 6; index++)
            {
                double angle = index * Math.PI / 3;
                var point = center + axis * 0.245
                    + (tangent * Math.Cos(angle) + bitangent * Math.Sin(angle)) * radius * 0.61;
                Beam(group, point, point + axis * 0.025, 0.026, "#52636F");
            }
        }

        /// <summary>分段二次曲线形成外置软管弯曲，不改变关节运动学。</summary>
        private static void CableCurve(Model3DGroup group, Point3D from, Point3D control,
            Point3D to, double radius)
        {
            var previous = from;
            for (int segment = 1; segment <= 8; segment++)
            {
                double t = segment / 8.0;
                var next = from + (control - from) * (2 * (1 - t) * t) + (to - from) * (t * t);
                Beam(group, previous, next, radius, "#2B3C45");
                previous = next;
            }
        }
    }
}
