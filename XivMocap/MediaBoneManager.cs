using Dalamud.Game.ClientState.Objects.Types;
using Ktisis.Structs.Actor;
using Ktisis.Structs;
using System.Numerics;
using Everything_To_IMU_SlimeVR.Tracking;
using FFXIVClientStructs.FFXIV.Client.Graphics;

namespace XivMocap.GameObjects
{
    public class MediaBoneManager
    {
        public static void CheckForValidBoneSounds(nint addresss)
        {
            unsafe
            {
                try
                {
                    Actor* characterActor = (Actor*)addresss;
                    var model = characterActor->Model;
                    if (model != null)
                    {
                        if (model != null && model->Skeleton != null)
                        {
                            for (int i = 0; i < model->Skeleton->PartialSkeletonCount; i++)
                            {
                                var partialSkeleton = model->Skeleton->PartialSkeletons[i];
                                var pos = partialSkeleton.GetHavokPose(0);
                                if (pos != null)
                                {
                                    var skeleton = pos->Skeleton;
                                    for (var i2 = 1; i2 < skeleton->Bones.Length; i2++)
                                    {
                                        if (model->Skeleton != null)
                                        {
                                            var bone = model->Skeleton->GetBone(i, i2);
                                            if (bone.HkaBone.Name.String != null)
                                            {
                                                if (!bone.IsBusted())
                                                {
                                                    //var worldPos = bone.GetWorldPos(characterActor, model);
                                                    var transform = bone.AccessModelSpace();
                                                    var matrix = Alloc.GetMatrix(transform);
                                                    //Alloc.SetMatrix(transform, Matrix4x4.Transform(matrix, new Vector3().ToQuaternion()));
                                                    //var boneObject = new MediaBoneObject(bone, characterActor, model);
                                                    string boneName = bone.HkaBone.Name.String;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {

                }
            }
        }
    }
}
