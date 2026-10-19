using System.Collections.Generic;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 把游戏内的 <see cref="Room"/> 采样成纯判定层可用的 <see cref="RoomSnapshot"/>。
    ///
    /// 采样只在这里发生一次：环境平均值按实际 cavity 格子计算，建筑与植物按
    /// 原版 <c>room.buildings</c>／<c>room.plants</c> 分类，不扫描全图、不按共享组件扩类。
    /// </summary>
    public static class RoomSnapshotFactory
    {
        private static readonly List<RoomCellSample> CellSamples = new List<RoomCellSample>();

        public static RoomSnapshot Create(Room room)
        {
            RoomSnapshot snapshot = new RoomSnapshot();
            if (room == null || room.cavity == null)
            {
                return snapshot;
            }

            snapshot.CellCount = room.cavity.NumCells;
            SampleCells(room.cavity.cells, snapshot);
            SampleBuildings(room.buildings, snapshot, isPlant: false);
            SampleBuildings(room.plants, snapshot, isPlant: true);
            SampleBuildings(room.otherEntities, snapshot, isPlant: false);

            return snapshot;
        }

        private static void SampleCells(List<int> cells, RoomSnapshot snapshot)
        {
            CellSamples.Clear();
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    int cell = cells[i];
                    if (!Grid.IsValidCell(cell))
                    {
                        continue;
                    }

                    Element element = Grid.Element[cell];
                    bool hasElement = element != null && !element.IsVacuum;
                    CellSamples.Add(new RoomCellSample
                    {
                        TemperatureK = Grid.Temperature[cell],
                        Decor = GameUtil.GetDecorAtCell(cell),
                        HasElement = hasElement
                    });
                }
            }

            snapshot.AverageTemperatureK = RoomEnvironment.AverageTemperature(CellSamples);
            snapshot.AverageDecor = RoomEnvironment.AverageDecor(CellSamples);
            CellSamples.Clear();
        }

        private static void SampleBuildings(List<KPrefabID> entities, RoomSnapshot snapshot, bool isPlant)
        {
            if (entities == null)
            {
                return;
            }

            for (int i = 0; i < entities.Count; i++)
            {
                KPrefabID entity = entities[i];
                if (entity == null)
                {
                    continue;
                }

                string prefabId = entity.PrefabID().ToString();

                if (isPlant)
                {
                    if (!entity.HasTag(GameTags.PlantBranch))
                    {
                        snapshot.PlantCount++;
                    }

                    snapshot.PrefabIds.Add(prefabId);
                    continue;
                }

                snapshot.BuildingCount++;
                snapshot.PrefabIds.Add(prefabId);

                if (entity.HasTag(RoomConstraints.ConstraintTags.IndustrialMachinery))
                {
                    snapshot.HasIndustrialMachinery = true;
                }

                if (RoomBuildingCatalogue.IsIndustrialBuilding(prefabId))
                {
                    snapshot.HasIndustrialBuilding = true;
                }

                if (RoomBuildingCatalogue.IsLifeBuilding(prefabId))
                {
                    snapshot.HasLifeBuilding = true;
                }

                if (entity.HasTag(RoomConstraints.ConstraintTags.ScienceBuilding))
                {
                    snapshot.SciencePrefabIds.Add(prefabId);
                }

                if (entity.HasTag(RoomConstraints.ConstraintTags.ToiletType))
                {
                    snapshot.HasToilet = true;
                }

                if (entity.HasTag(RoomConstraints.ConstraintTags.DiningTableType))
                {
                    snapshot.HasDiningTable = true;
                }

                if (entity.HasTag(GameTags.Decoration))
                {
                    snapshot.HasDecoration = true;
                }

                Storage storage = entity.GetComponent<Storage>();
                if (storage != null)
                {
                    snapshot.HasStorage = true;
                }

                Light2D light = entity.GetComponent<Light2D>();
                if (light != null && light.enabled)
                {
                    snapshot.HasLight = true;
                }

                OrnamentReceptacle receptacle = entity.GetComponent<OrnamentReceptacle>();
                if (receptacle != null && receptacle.IsHoldingOrnament && receptacle.IsOperational)
                {
                    snapshot.HasOrnamentDisplayed = true;
                }

                if (HasMasterpiece(entity))
                {
                    snapshot.HasMasterpiece = true;
                }
            }
        }

        private static bool HasMasterpiece(KPrefabID entity)
        {
            Artable artable = entity.GetComponent<Artable>();
            if (artable == null || string.IsNullOrEmpty(artable.CurrentStage) || artable.CurrentStage == "Default")
            {
                return false;
            }

            Database.ArtableStage stage = Db.GetArtableStages().TryGet(artable.CurrentStage);
            return stage != null
                && stage.statusItem != null
                && stage.statusItem.StatusType == Database.ArtableStatuses.ArtableStatusType.LookingGreat;
        }
    }
}
