window.mapInterop = {
    _instances: {},

    init(elementId, lat, lng, radiusKm, dotNetRef) {
        if (this._instances[elementId]) {
            this._instances[elementId].map.remove();
        }

        const map = L.map(elementId).setView([lat, lng], 8);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 18
        }).addTo(map);

        const marker = L.marker([lat, lng], { draggable: true }).addTo(map);
        const circle = L.circle([lat, lng], {
            radius: radiusKm * 1000,
            color: '#1976d2',
            fillColor: '#1976d2',
            fillOpacity: 0.12,
            weight: 2
        }).addTo(map);

        const edgeIcon = L.divIcon({
            className: '',
            html: '<div style="width:14px;height:14px;background:#1976d2;border:2px solid white;border-radius:50%;cursor:ew-resize;box-shadow:0 1px 4px rgba(0,0,0,0.5)"></div>',
            iconSize: [14, 14],
            iconAnchor: [7, 7]
        });
        const edgeMarker = L.marker(edgeLatLng(lat, lng, radiusKm), {
            draggable: true,
            icon: edgeIcon
        }).addTo(map);

        map.fitBounds(circle.getBounds());

        marker.on('dragend', () => {
            const pos = marker.getLatLng();
            const r = circle.getRadius() / 1000;
            circle.setLatLng(pos);
            edgeMarker.setLatLng(edgeLatLng(pos.lat, pos.lng, r));
            dotNetRef.invokeMethodAsync('OnMarkerMoved', pos.lat, pos.lng);
        });

        edgeMarker.on('drag', () => {
            const ePos = edgeMarker.getLatLng();
            const cPos = marker.getLatLng();
            const newRadius = haversineKm(cPos.lat, cPos.lng, ePos.lat, ePos.lng);
            circle.setRadius(newRadius * 1000);
            dotNetRef.invokeMethodAsync('OnRadiusChanged', Math.round(newRadius));
        });

        edgeMarker.on('dragend', () => {
            const cPos = marker.getLatLng();
            const r = circle.getRadius() / 1000;
            edgeMarker.setLatLng(edgeLatLng(cPos.lat, cPos.lng, r));
            map.fitBounds(circle.getBounds());
        });

        this._instances[elementId] = { map, marker, circle, edgeMarker };
    },

    updateRadius(elementId, radiusKm) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.circle.setRadius(radiusKm * 1000);
        const cPos = inst.marker.getLatLng();
        inst.edgeMarker.setLatLng(edgeLatLng(cPos.lat, cPos.lng, radiusKm));
        inst.map.fitBounds(inst.circle.getBounds());
    },

    destroy(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.map.remove();
        delete this._instances[elementId];
    }
};

function edgeLatLng(lat, lng, radiusKm) {
    const lngDelta = radiusKm / (111.32 * Math.cos(lat * Math.PI / 180));
    return [lat, lng + lngDelta];
}

function haversineKm(lat1, lng1, lat2, lng2) {
    const R = 6371;
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) ** 2
            + Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180)
            * Math.sin(dLng / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}
