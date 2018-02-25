using CocosSharp;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CocosSharp.Spine
{
    public enum AttachmentType
    {
        ATTACHMENT_REGION = 1, ATTACHMENT_REGION_SEQUENCE = 2, ATTACHMENT_BOUNDING_BOX = 3
    }

    public class CCSkeleton : CCNode
    {
        public float FLT_MAX = 3.402823466e+38F;     /* max value */
        public float FLT_MIN = 1.175494351e-38F;     /* min positive value */

        public static int ATTACHMENT_REGION = 0;
        public static int ATTACHMENT_REGION_SEQUENCE = 1;
        public static int ATTACHMENT_BOUNDING_BOX = 2;

        public CCGeometryNode skeletonGeometry;
        public Skeleton Skeleton { get; private set; }
        public bool PremultipliedAlpha { get; set; }
        public string ImagesDirectory { get; set; }
        SkeletonClipping clipper = new SkeletonClipping();

        bool ownsSkeletonData;
        Atlas atlas;

        float[] vertices = new float[8];
        int[] quadTriangles = { 0, 1, 2, 2, 3, 0 };

        const int TL = 0;
        const int TR = 1;
        const int BL = 2;
        const int BR = 3;

        public void SetSkeletonData(SkeletonData skeletonData, bool ownsSkeletonData)
        {
            Skeleton = new Skeleton(skeletonData);
            this.ownsSkeletonData = ownsSkeletonData;
        }

        public CCSkeleton()
        {
            Initialize();
        }

        public CCSkeleton(SkeletonData skeletonData, bool ownsSkeletonData = false)
        {
            Initialize();
            SetSkeletonData(skeletonData, ownsSkeletonData);
        }

        public CCSkeleton(string skeletonDataFile, Atlas atlas, float scale = 0)
        {
            var json = new SkeletonJson(atlas);
            json.Scale = scale == 0 ? 1 : scale;
            SkeletonData skeletonData = json.ReadSkeletonData(skeletonDataFile);
            SetSkeletonData(skeletonData, true);
        }

        public CCSkeleton(string skeletonDataFile, string atlasFile, float scale = 0)
        {

            Initialize();

            using (StreamReader atlasStream = new StreamReader(CCFileUtils.GetFileStream(atlasFile)))
            {
                atlas = new Atlas(atlasStream, "", new CocosSharpTextureLoader());
            }

            SkeletonJson json = new SkeletonJson(atlas);
            json.Scale = scale == 0 ? 1 : scale;

            using (StreamReader skeletonDataStream = new StreamReader(CCFileUtils.GetFileStream(skeletonDataFile)))
            {
                SkeletonData skeletonData = json.ReadSkeletonData(skeletonDataStream);
                skeletonData.Name = skeletonDataFile;
                SetSkeletonData(skeletonData, true);
            }
        }

        public void Initialize()
        {
            atlas = null;
            PremultipliedAlpha = false;
            ImagesDirectory = string.Empty;
            skeletonGeometry = new CCGeometryNode();
            OpacityModifyRGB = true;
            AddChild(skeletonGeometry);
            Schedule();
        }

        ~CCSkeleton()
        {
            CCLog.Log("finalize");

        }

        public override void Update(float dt)
        {
            base.Update(dt);
            UpdateSkeletonGeometry();
        }

        public void UpdateSkeletonGeometry()
        {
            skeletonGeometry.ClearInstances();
            BlendState blend;
            var drawOrder = Skeleton.DrawOrder;
            var drawOrderItems = Skeleton.DrawOrder.Items;
            float skeletonR = Skeleton.R, skeletonG = Skeleton.G, skeletonB = Skeleton.B, skeletonA = Skeleton.A;
            Color color;

            for (int i = 0, n = drawOrder.Count; i < n; i++)
            {
                Slot slot = drawOrderItems[i];
                Attachment attachment = slot.Attachment;
                blend = slot.Data.BlendMode == BlendMode.Additive ? BlendState.Additive :
                            PremultipliedAlpha ? BlendState.AlphaBlend : BlendState.NonPremultiplied;
                float attachmentColorR, attachmentColorG, attachmentColorB, attachmentColorA;
                CCTexture2D texture = null;
                int verticesCount = 0;
                //float[] vertices = this.vertices;
                int indicesCount = 0;
                int[] indices = null;
                float[] uvs = null;

                if (attachment is RegionAttachment)
                {
                    RegionAttachment regionAttachment = (RegionAttachment)attachment;
                    attachmentColorR = regionAttachment.R; attachmentColorG = regionAttachment.G; attachmentColorB = regionAttachment.B; attachmentColorA = regionAttachment.A;
                    AtlasRegion region = (AtlasRegion)regionAttachment.RendererObject;
                    texture = (CCTexture2D)region.page.rendererObject;
                    verticesCount = 4;
                    regionAttachment.ComputeWorldVertices(slot.Bone, vertices, 0, 2);
                    indicesCount = 6;
                    indices = quadTriangles;
                    uvs = regionAttachment.UVs;
                }
                else if (attachment is MeshAttachment)
                {
                    MeshAttachment mesh = (MeshAttachment)attachment;
                    attachmentColorR = mesh.R; attachmentColorG = mesh.G; attachmentColorB = mesh.B; attachmentColorA = mesh.A;
                    AtlasRegion region = (AtlasRegion)mesh.RendererObject;
                    texture = (CCTexture2D)region.page.rendererObject;
                    int vertexCount = mesh.WorldVerticesLength;
                    if (vertices.Length < vertexCount) vertices = new float[vertexCount];
                    verticesCount = vertexCount >> 1;
                    mesh.ComputeWorldVertices(slot, vertices);
                    indicesCount = mesh.Triangles.Length;
                    indices = mesh.Triangles;
                    uvs = mesh.UVs;
                }
                else if (attachment is ClippingAttachment)
                {
                    ClippingAttachment clip = (ClippingAttachment)attachment;
                    clipper.ClipStart(slot, clip);
                    continue;
                }
                else
                {
                    continue;
                }

                // calculate color
                float a = skeletonA * slot.A * attachmentColorA;
                if (PremultipliedAlpha)
                {
                    color = new Color(
                            skeletonR * slot.R * attachmentColorR * a,
                            skeletonG * slot.G * attachmentColorG * a,
                            skeletonB * slot.B * attachmentColorB * a, a);
                }
                else
                {
                    color = new Color(
                            skeletonR * slot.R * attachmentColorR,
                            skeletonG * slot.G * attachmentColorG,
                            skeletonB * slot.B * attachmentColorB, a);
                }

                //Color darkColor = new Color();
                //if (slot.HasSecondColor)
                //{
                //      darkColor = new Color(slot.R2 * a, slot.G2 * a, slot.B2 * a);
                //}
                //darkColor.A = PremultipliedAlpha ? (byte)255 : (byte)0;

                // clip
                if (clipper.IsClipping)
                {
                    clipper.ClipTriangles(vertices, verticesCount << 1, indices, indicesCount, uvs);
                    vertices = clipper.ClippedVertices.Items;
                    verticesCount = clipper.ClippedVertices.Count >> 1;
                    indices = clipper.ClippedTriangles.Items;
                    indicesCount = clipper.ClippedTriangles.Count;
                    uvs = clipper.ClippedUVs.Items;
                }

                if (verticesCount == 0 || indicesCount == 0)
                    continue;

                // submit to batch
                var item = skeletonGeometry.CreateGeometryInstance(verticesCount, indicesCount);
                item.InstanceAttributes.BlendState = blend;
                item.GeometryPacket.Texture = texture;
                for (int ii = 0, nn = indicesCount; ii < nn; ii++)
                {
                    item.GeometryPacket.Indicies[ii] = indices[ii];
                }

                var itemVertices = item.GeometryPacket.Vertices;
                for (int ii = 0, v = 0, nn = verticesCount << 1; v < nn; ii++, v += 2)
                {
                    itemVertices[ii].Colors = new CCColor4B(color.R, color.G, color.B, color.A);
                    //itemVertices[ii].Colors2 = new CCColor4B(darkColor.R, darkColor.G, darkColor.B, darkColor.A);
                    itemVertices[ii].Vertices.X = vertices[v];
                    itemVertices[ii].Vertices.Y = vertices[v + 1];
                    itemVertices[ii].Vertices.Z = 0;
                    itemVertices[ii].TexCoords.U = uvs[v];
                    itemVertices[ii].TexCoords.V = uvs[v + 1];
                }

                clipper.ClipEnd(slot);
            }
            clipper.ClipEnd();
        }

        public override CCSize ContentSize
        {
            get
            {
                var bbox = boundingBox();
                return new CCSize(bbox.Size.Width, bbox.Size.Height);
            }
        }

        CCRect boundingBox()
        {
            float minX = FLT_MAX, minY = FLT_MAX, maxX = FLT_MIN, maxY = FLT_MIN;

            for (int i = 0; i < Skeleton.Slots.Count; ++i)
            {

                var slot = Skeleton.Slots.Items[i];

                if (slot.Attachment == null) continue;

                var verticesCount = 0;
                var worldVertices = new float[1000]; // Max number of vertices per mesh.
                if (slot.Attachment is RegionAttachment)
                {
                    var attachment = (RegionAttachment)slot.Attachment;
                    attachment.ComputeWorldVertices(slot.bone, worldVertices, 0, 2);
                    verticesCount = 8;
                }
                else if (slot.Attachment is MeshAttachment)
                {
                    var mesh = (MeshAttachment)slot.Attachment;
                    mesh.ComputeWorldVertices(slot, worldVertices);
                    verticesCount = mesh.Vertices.Length;
                }
                else
                    continue;
                for (int ii = 0; ii < verticesCount; ii += 2)
                {
                    float x = worldVertices[ii] * ScaleX, y = worldVertices[ii + 1] * ScaleY;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }

            }
            CCPoint position = Position;

            return new CCRect(position.X + minX, position.Y + minY, maxX - minX, maxY - minY);
        }

        // --- Convenience methods for common Skeleton_* functions.
        public void UpdateWorldTransform()
        {
            Skeleton.UpdateWorldTransform();
        }

        public void SetToSetupPose()
        {
            Skeleton.SetToSetupPose();
        }
        public void SetBonesToSetupPose()
        {
            Skeleton.SetBonesToSetupPose();
        }
        public void SetSlotsToSetupPose()
        {
            Skeleton.SetSlotsToSetupPose();
        }

        /* Returns 0 if the bone was not found. */
        public Bone FindBone(string boneName)
        {
            return Skeleton.FindBone(boneName);
        }
        /* Returns 0 if the slot was not found. */
        public Slot FindSlot(string slotName)
        {
            return Skeleton.FindSlot(slotName);
        }

        /* Sets the skin used to look up attachments not found in the SkeletonData defaultSkin. Attachments from the new skin are
         * attached if the corresponding attachment from the old skin was attached. Returns false if the skin was not found.
         * @param skin May be 0.*/
        public bool SetSkin(string skinName)
        {
            Skeleton.SetSkin(skinName);
            return true;
        }

        /* Returns 0 if the slot or attachment was not found. */
        public Attachment GetAttachment(string slotName, string attachmentName)
        {
            return Skeleton.GetAttachment(slotName, attachmentName);
        }
        /* Returns false if the slot or attachment was not found. */
        public bool SetAttachment(string slotName, string attachmentName)
        {
            Skeleton.SetAttachment(slotName, attachmentName);
            return true;
        }

        public bool OpacityModifyRGB
        {
            get
            {
                return PremultipliedAlpha;
            }

            set
            {
                PremultipliedAlpha = value;
            }
        }

        //virtual cocos2d::CCTextureAtlas* getTextureAtlas (RegionAttachment regionAttachment);
        #region SpinesCocos2d

        void UpdateRegionAttachmentQuad(RegionAttachment self, Slot slot, ref CCV3F_C4B_T2F_Quad quad, bool premultipliedAlpha = false)
        {
            self.ComputeWorldVertices(slot.Bone, vertices, 0, 2);

            float r = slot.Skeleton.R * slot.R * 255;
            float g = slot.Skeleton.G * slot.G * 255;
            float b = slot.Skeleton.B * slot.B * 255;

            float normalizedAlpha = slot.Skeleton.A * slot.A;
            if (premultipliedAlpha)
            {
                r *= normalizedAlpha;
                g *= normalizedAlpha;
                b *= normalizedAlpha;
            }

            float a = normalizedAlpha * 255;
            quad.BottomLeft.Colors.R = (byte)r;
            quad.BottomLeft.Colors.G = (byte)g;
            quad.BottomLeft.Colors.B = (byte)b;
            quad.BottomLeft.Colors.A = (byte)a;
            quad.TopLeft.Colors.R = (byte)r;
            quad.TopLeft.Colors.G = (byte)g;
            quad.TopLeft.Colors.B = (byte)b;
            quad.TopLeft.Colors.A = (byte)a;
            quad.TopRight.Colors.R = (byte)r;
            quad.TopRight.Colors.G = (byte)g;
            quad.TopRight.Colors.B = (byte)b;
            quad.TopRight.Colors.A = (byte)a;
            quad.BottomRight.Colors.R = (byte)r;
            quad.BottomRight.Colors.G = (byte)g;
            quad.BottomRight.Colors.B = (byte)b;
            quad.BottomRight.Colors.A = (byte)a;

            quad.BottomLeft.Vertices.X = vertices[RegionAttachment.BLX];
            quad.BottomLeft.Vertices.Y = vertices[RegionAttachment.BLY];
            quad.TopLeft.Vertices.X = vertices[RegionAttachment.ULX];
            quad.TopLeft.Vertices.Y = vertices[RegionAttachment.ULY];
            quad.TopRight.Vertices.X = vertices[RegionAttachment.URX];
            quad.TopRight.Vertices.Y = vertices[RegionAttachment.URY];
            quad.BottomRight.Vertices.X = vertices[RegionAttachment.BRX];
            quad.BottomRight.Vertices.Y = vertices[RegionAttachment.BRY];

            quad.BottomLeft.TexCoords.U = self.UVs[RegionAttachment.BLX];
            quad.BottomLeft.TexCoords.V = self.UVs[RegionAttachment.BLY];
            quad.TopLeft.TexCoords.U = self.UVs[RegionAttachment.ULX];
            quad.TopLeft.TexCoords.V = self.UVs[RegionAttachment.ULY];
            quad.TopRight.TexCoords.U = self.UVs[RegionAttachment.URX];
            quad.TopRight.TexCoords.V = self.UVs[RegionAttachment.URY];
            quad.BottomRight.TexCoords.U = self.UVs[RegionAttachment.BRX];
            quad.BottomRight.TexCoords.V = self.UVs[RegionAttachment.BRY];
        }

        #endregion
    }
}