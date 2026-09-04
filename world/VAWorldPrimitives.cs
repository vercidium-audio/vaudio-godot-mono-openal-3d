namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    void AddPrimitive(Node node, vaudio.MaterialType material, bool recursive) =>
        AddPrimitive(node, material, false, PropagateMode.All, recursive);

    void AddPrimitive(Node node, vaudio.MaterialType material, bool useFlatTransmission, PropagateMode filter, bool recursive)
    {
        bool hasOwnMaterial = node.HasMeta(MATERIAL_META_KEY);

        if (hasOwnMaterial)
        {
            // A node's own material always wins
            material = GetMaterial(node);
        }

        // Get this node's filter (if any). Defaults to the inherited filter
        filter = ReadPropagateMode(node, filter);

        // Use this specific transmission setting rather than the parent's
        if (node.HasMeta(USE_FLAT_TRANSMISSION_META_KEY))
            useFlatTransmission = node.GetMeta(USE_FLAT_TRANSMISSION_META_KEY).As<bool>();

        // Ignore nodes without materials
        if (material != vaudio.MaterialType.Air)
        {
            if (hasOwnMaterial || PassesPropagationFilter(node, filter))
            {
                if (node is CsgBox3D csgBox)
                    CreateVAudioPrimitive(csgBox, material);
                else if (node is CsgCylinder3D csgCylinder)
                    CreateVAudioPrimitive(csgCylinder, material);
                else if (node is CsgSphere3D csgSphere)
                    CreateVAudioPrimitive(csgSphere, material);
                else if (node is CsgPolygon3D csgPolygon)
                    CreateVAudioPrimitive(csgPolygon, material);
                else if (node is CsgMesh3D csgMesh)
                    CreateVAudioPrimitive(csgMesh, material);
                else if (node is CollisionShape3D collisionShape)
                    CreateVAudioPrimitive(collisionShape, material);
                else if (node is MeshInstance3D meshInstance)
                    CreateVAudioPrimitive(meshInstance, material, useFlatTransmission);
            }
        }

        if (recursive)
            foreach (Node child in node.GetChildren())
                AddPrimitive(child, material, useFlatTransmission, filter, true);
    }

    // Check whether a cascading material should reach this node
    bool PassesPropagationFilter(Node node, PropagateMode filter)
    {
        bool isCollider = node is CollisionShape3D;

        switch (filter)
        {
            case PropagateMode.Colliders when !isCollider:
                return false;
            case PropagateMode.Visuals when isCollider:
                return false;
        }

        // The VAWorld's render/collision layer masks control which nodes an inherited material are applied to
        if (node is VisualInstance3D visual)
            return (visual.Layers & RenderLayers) != 0;

        if (isCollider && node.GetParentOrNull<CollisionObject3D>() is { } body)
            return (body.CollisionLayer & CollisionLayers) != 0;

        return true;
    }

    public void SyncPrimitive(Node node)
    {
        if (world == null)
            return;

        RemovePrimitive(node, true);
        AddPrimitive(node, vaudio.MaterialType.Air, true, PropagateMode.All, true);
    }

    // Re-evaluate every node against the current layer masks. Invoked when the VAWorld RenderLayers / CollisionLayers settings change at runtime
    void RebuildPrimitives()
    {
        // Godot runs [Export] setters during scene deserialization, before _EnterTree. This world is still null then, so bail early
        if (world == null || !IsInsideTree())
            return;

        Node root = GetTree().Root;

        if (root == null)
            return;

        foreach (var child in root.GetChildren())
        {
            RemovePrimitive(child, true);
            AddPrimitive(child, vaudio.MaterialType.Air, true);
        }
    }

    void RemovePrimitive(Node node, bool recursive)
    {
        // When a node is removed from the scene, remove it from the raytracing simulation too
        if (node.HasMeta(PRIMITIVE_META_KEY))
        {
            var wrapper = node.GetMeta(PRIMITIVE_META_KEY).As<VAPrimitiveRef>();

            wrapper.Watcher?.QueueFree();

            if (wrapper.ShapeCallable is Callable shapeCallable && node is CollisionShape3D cs && cs.Shape != null)
                if (cs.Shape.IsConnected(Resource.SignalName.Changed, shapeCallable))
                    cs.Shape.Disconnect(Resource.SignalName.Changed, shapeCallable);

            world.RemovePrimitive(wrapper.Primitive);
            node.RemoveMeta(PRIMITIVE_META_KEY);
        }

        if (recursive)
            foreach (Node child in node.GetChildren())
                RemovePrimitive(child, true);
    }

    static VAPrimitiveRef AttachWatcher(Node3D node, vaudio.Primitive prim, Action update)
    {
        var watcher = new TransformWatcher { OnTransformChanged = update };
        node.AddChild(watcher);
        return new VAPrimitiveRef { Primitive = prim, Watcher = watcher };
    }

    // PrismPrimitive only supports rotation/translation, not scale, so any scale present in the
    //  transform's basis must be pre-applied to the size and stripped from the returned transform
    static Transform3D RemoveScale(Transform3D transform, out Vector3 scale)
    {
        scale = transform.Basis.Scale;
        return new Transform3D(transform.Basis.Orthonormalized(), transform.Origin);
    }

    void CreateVAudioPrimitive(CsgBox3D csgBox, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (csgBox.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        var transform = RemoveScale(csgBox.GlobalTransform, out var scale);

        vaudio.PrismPrimitive prim = new()
        {
            size = ToVAudio(csgBox.Size * scale),
            transform = ToVAudio(transform),
            material = material
        };

        world.AddPrimitive(prim);

        csgBox.SetMeta(PRIMITIVE_META_KEY, AttachWatcher(csgBox, prim, () =>
        {
            var updatedTransform = RemoveScale(csgBox.GlobalTransform, out var updatedScale);

            prim.size = ToVAudio(csgBox.Size * updatedScale);
            prim.transform = ToVAudio(updatedTransform);
        }));
    }

    void CreateVAudioPrimitive(CsgCylinder3D csgCylinder, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (csgCylinder.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        // CsgCylinder3D can be either a cylinder or a cone depending on the Cone property
        vaudio.Primitive prim;

        if (csgCylinder.Cone)
        {
            var globalTransform = csgCylinder.GlobalTransform;
            var offsetTransform = globalTransform.TranslatedLocal(new Vector3(0, -csgCylinder.Height / 2, 0));

            prim = new vaudio.ConePrimitive()
            {
                radius = csgCylinder.Radius,
                height = csgCylinder.Height,
                transform = ToVAudio(offsetTransform),
                material = material
            };
        }
        else
        {
            prim = new vaudio.CylinderPrimitive()
            {
                radius = csgCylinder.Radius,
                length = csgCylinder.Height,
                transform = ToVAudio(csgCylinder.GlobalTransform),
                material = material
            };
        }

        world.AddPrimitive(prim);

        csgCylinder.SetMeta(PRIMITIVE_META_KEY, AttachWatcher(csgCylinder, prim, () =>
        {
            if (prim is vaudio.ConePrimitive conePrim)
            {
                var globalTransform = csgCylinder.GlobalTransform;
                var offsetTransform = globalTransform.TranslatedLocal(new Vector3(0, -csgCylinder.Height / 2, 0));
                conePrim.radius = csgCylinder.Radius;
                conePrim.height = csgCylinder.Height;
                conePrim.transform = ToVAudio(offsetTransform);
            }
            else if (prim is vaudio.CylinderPrimitive cylinderPrim)
            {
                cylinderPrim.radius = csgCylinder.Radius;
                cylinderPrim.length = csgCylinder.Height;
                cylinderPrim.transform = ToVAudio(csgCylinder.GlobalTransform);
            }
        }));
    }

    void CreateVAudioPrimitive(CsgSphere3D csgSphere, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (csgSphere.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        var globalTransform = csgSphere.GlobalTransform;

        vaudio.SpherePrimitive prim = new()
        {
            center = ToVAudio(globalTransform.Origin),
            radius = csgSphere.Radius,
            material = material
        };

        world.AddPrimitive(prim);

        csgSphere.SetMeta(PRIMITIVE_META_KEY, AttachWatcher(csgSphere, prim, () =>
        {
            prim.center = ToVAudio(csgSphere.GlobalTransform.Origin);
            prim.radius = csgSphere.Radius;
        }));
    }

    void CreateVAudioPrimitive(CsgPolygon3D csgPolygon, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (csgPolygon.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        // CsgPolygon3D generates a mesh from a 2D polygon extruded/spun in 3D
        // GetMeshes() returns an array of [Transform3D, Mesh] pairs
        var meshes = csgPolygon.GetMeshes();
        if (meshes == null || meshes.Count < 2)
        {
            LogWarning($"CsgPolygon3D {csgPolygon.Name} will not affect rayracing as it has no mesh");
            return;
        }

        // The mesh is at index 1 (index 0 is the transform)
        var mesh = meshes[1].As<Mesh>();
        if (mesh == null)
        {
            LogWarning($"CsgPolygon3D {csgPolygon.Name} will not affect rayracing as it's mesh is invalid");
            return;
        }

        var triangles = Conversions.ConvertMeshToVector3FList(csgPolygon.Name, mesh, out var min, out var max, this);

        if (triangles.Count == 0)
            return;

        var transform = ToVAudio(csgPolygon.GlobalTransform);

        vaudio.MeshPrimitive prim = new(material, triangles, min, max, transform);

        world.AddPrimitive(prim);

        csgPolygon.SetMeta(PRIMITIVE_META_KEY, AttachWatcher(csgPolygon, prim, () =>
        {
            prim.transform = ToVAudio(csgPolygon.GlobalTransform);
        }));
    }

    void CreateVAudioPrimitive(CsgMesh3D csgMesh, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (csgMesh.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        // CsgMesh3D has a Mesh property that can be used directly
        var mesh = csgMesh.Mesh;
        if (mesh == null)
        {
            LogWarning($"CsgMesh3D {csgMesh.Name} will not affect rayracing as it has no mesh");
            return;
        }

        var triangles = Conversions.ConvertMeshToVector3FList(csgMesh.Name, mesh, out var min, out var max, this);

        if (triangles.Count == 0)
            return;

        var transform = ToVAudio(csgMesh.GlobalTransform);

        vaudio.MeshPrimitive prim = new(material, triangles, min, max, transform);

        world.AddPrimitive(prim);

        csgMesh.SetMeta(PRIMITIVE_META_KEY, AttachWatcher(csgMesh, prim, () =>
        {
            prim.transform = ToVAudio(csgMesh.GlobalTransform);
        }));
    }

    void CreateVAudioPrimitive(CollisionShape3D collisionShape, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (collisionShape.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        var shape = collisionShape.Shape;
        var globalTransform = collisionShape.GlobalTransform;
        var position = globalTransform.Origin;
        var scale = collisionShape.Scale;

        // Create primitive based on shape type
        vaudio.Primitive prim = null;

        if (shape is BoxShape3D box)
        {
            var boxTransform = RemoveScale(globalTransform, out var boxScale);

            world.AddPrimitive(prim = new vaudio.PrismPrimitive()
            {
                size = ToVAudio(box.Size * boxScale),
                transform = ToVAudio(boxTransform),
                material = material
            });
        }
        else if (shape is SphereShape3D sphere)
        {
            world.AddPrimitive(prim = new vaudio.SpherePrimitive()
            {
                center = new vaudio.Vector(position.X, position.Y, position.Z),
                radius = sphere.Radius * scale.X,
                material = material
            });
        }
        else if (shape is CapsuleShape3D capsule)
        {
            var capsuleTransform = RemoveScale(globalTransform, out var capsuleScale);

            float cylinderLength = capsule.Height - 2 * capsule.Radius;
            if (cylinderLength < 0) cylinderLength = 0;

            world.AddPrimitive(prim = new vaudio.CapsulePrimitive()
            {
                radius = capsule.Radius * capsuleScale.X,
                length = cylinderLength * capsuleScale.Y,
                transform = ToVAudio(capsuleTransform),
                material = material
            });
        }
        else if (shape is CylinderShape3D cylinder)
        {
            var cylinderTransform = RemoveScale(globalTransform, out var cylinderScale);

            world.AddPrimitive(prim = new vaudio.CylinderPrimitive()
            {
                radius = cylinder.Radius * cylinderScale.X,
                length = cylinder.Height * cylinderScale.Y,
                transform = ToVAudio(cylinderTransform),
                material = material
            });
        }
        else if (shape is WorldBoundaryShape3D worldBoundary)
        {
            // WorldBoundaryShape3D represents an infinite plane, we approximate with a large plane
            var plane = worldBoundary.Plane;
            var normal = plane.Normal;

            // vaudio.PlanePrimitive lies in XZ plane at Y=0 in local space, with Y-up as the normal
            // So we need basisY to be the plane normal
            var basisY = new Vector3(normal.X, normal.Y, normal.Z);
            var basisX = basisY.Cross(Vector3.Forward).Normalized();

            if (basisX.LengthSquared() < 0.001f)
                basisX = basisY.Cross(Vector3.Right).Normalized();

            var basisZ = basisX.Cross(basisY).Normalized();

            // The plane position is: point on plane (normal * D) + the collision shape's global position
            var planePosition = normal * plane.D + globalTransform.Origin;

            var planeTransform = new Transform3D(
                new Basis(basisX, basisY, basisZ),
                planePosition
            );

            var worldMagnitude = world.Size.Magnitude;

            world.AddPrimitive(prim = new vaudio.PlanePrimitive()
            {
                // Use the max world size to ensure the plane covers the raytracing scene
                //  * 2 in case the plane is positioned in the corner of the world
                width = worldMagnitude * 2,
                height = worldMagnitude * 2,
                transform = ToVAudio(planeTransform),
                material = material
            });
        }
        else if (shape is ConvexPolygonShape3D convexPolygon)
        {
            var triangles = Conversions.ConvertConvexPolygonToVector3FList(collisionShape.Name, convexPolygon, out var min, out var max, this);
            var transform = ToVAudio(globalTransform);

            if (triangles.Count > 0)
            {
                prim = new vaudio.MeshPrimitive(material, triangles, min, max, transform);
                world.AddPrimitive(prim);
            }
        }
        else if (shape is HeightMapShape3D heightMap)
        {
            var triangles = Conversions.ConvertHeightMapToVector3FList(collisionShape.Name, heightMap, out var min, out var max, this);
            var transform = ToVAudio(globalTransform);

            if (triangles.Count > 0)
            {
                prim = new vaudio.MeshPrimitive(material, triangles, min, max, transform);
                world.AddPrimitive(prim);
            }
        }
        else if (shape is ConcavePolygonShape3D polygon)
        {
            var triangles = Conversions.ConvertConcavePolygonToVector3FList(collisionShape.Name, polygon, out var min, out var max, this);
            var transform = ToVAudio(globalTransform);

            if (triangles.Count > 0)
            {
                prim = new vaudio.MeshPrimitive(material, triangles, min, max, transform);
                world.AddPrimitive(prim);
            }
        }

        // Store the primitive on the collision shape, so we can update it later if it moves
        if (prim != null)
        {
            void update() => UpdateCollisionShapePrimitive(collisionShape, prim);
            var wrapper = AttachWatcher(collisionShape, prim, update);

            if (collisionShape.Shape is BoxShape3D box2)
            {
                var callable = Callable.From(update);
                box2.Connect(Resource.SignalName.Changed, callable);
                wrapper.ShapeCallable = callable;
            }

            collisionShape.SetMeta(PRIMITIVE_META_KEY, wrapper);
        }
    }

    static void UpdateCollisionShapePrimitive(CollisionShape3D collisionShape, vaudio.Primitive primitive)
    {
        var globalTransform = collisionShape.GlobalTransform;

        // Update position/transform of vaudio primitives
        if (primitive is vaudio.MeshPrimitive mesh)
        {
            mesh.transform = ToVAudio(globalTransform);
        }
        else if (primitive is vaudio.SpherePrimitive sphere)
        {
            sphere.center = ToVAudio(globalTransform.Origin);
            sphere.radius = (collisionShape.Shape as SphereShape3D).Radius * collisionShape.Scale.X;
        }
        else if (primitive is vaudio.PrismPrimitive prism)
        {
            var box = collisionShape.Shape as BoxShape3D;
            var boxTransform = RemoveScale(globalTransform, out var boxScale);

            prism.size = ToVAudio(box.Size * boxScale);
            prism.transform = ToVAudio(boxTransform);
        }
        else if (primitive is vaudio.CapsulePrimitive capsulePrim)
        {
            var capsule = collisionShape.Shape as CapsuleShape3D;
            var capsuleTransform = RemoveScale(globalTransform, out var capsuleScale);

            float cylinderLength = capsule.Height - 2 * capsule.Radius;
            if (cylinderLength < 0) cylinderLength = 0;

            capsulePrim.radius = capsule.Radius * capsuleScale.X;
            capsulePrim.length = cylinderLength * capsuleScale.Y;
            capsulePrim.transform = ToVAudio(capsuleTransform);
        }
        else if (primitive is vaudio.CylinderPrimitive cylinderPrim)
        {
            var cylinder = collisionShape.Shape as CylinderShape3D;
            var cylinderTransform = RemoveScale(globalTransform, out var cylinderScale);

            cylinderPrim.radius = cylinder.Radius * cylinderScale.X;
            cylinderPrim.length = cylinder.Height * cylinderScale.Y;
            cylinderPrim.transform = ToVAudio(cylinderTransform);
        }
        else if (primitive is vaudio.PlanePrimitive planePrim)
        {
            var worldBoundary = collisionShape.Shape as WorldBoundaryShape3D;
            var plane = worldBoundary.Plane;
            var normal = plane.Normal;

            // VAudio PlanePrimitive lies in XZ plane at Y=0 in local space, with Y-up as the normal
            var basisY = new Vector3(normal.X, normal.Y, normal.Z);
            var basisX = basisY.Cross(Vector3.Forward).Normalized();
            if (basisX.LengthSquared() < 0.001f)
                basisX = basisY.Cross(Vector3.Right).Normalized();
            var basisZ = basisX.Cross(basisY).Normalized();

            // The plane position is: point on plane (normal * D) + the collision shape's global position
            var planePosition = normal * plane.D + globalTransform.Origin;

            var planeTransform = new Transform3D(
                new Basis(basisX, basisY, basisZ),
                planePosition
            );

            planePrim.transform = ToVAudio(planeTransform);
        }
    }

    void CreateVAudioPrimitive(MeshInstance3D meshInstance, vaudio.MaterialType material, bool useFlatTransmission)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (meshInstance.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        var mesh = meshInstance.Mesh;
        if (mesh == null)
        {
            LogWarning($"MeshInstance3D {meshInstance.Name} will not affect rayracing as it has no mesh");
            return;
        }

        // Convert mesh to triangle list
        var triangles = Conversions.ConvertMeshToVector3FList(meshInstance.Name, mesh, out var min, out var max, this);

        if (triangles.Count == 0)
            return;

        var transform = ToVAudio(meshInstance.GlobalTransform);

        vaudio.MeshPrimitive prim = new(material, triangles, min, max, transform)
        {
            UseFlatTransmission = useFlatTransmission
        };

        world.AddPrimitive(prim);

        meshInstance.SetMeta(PRIMITIVE_META_KEY, AttachWatcher(meshInstance, prim, () =>
        {
            prim.transform = ToVAudio(meshInstance.GlobalTransform);
        }));
    }

}
