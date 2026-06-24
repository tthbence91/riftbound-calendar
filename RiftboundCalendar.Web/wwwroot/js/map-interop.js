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

        map.fitBounds(circle.getBounds());

        marker.on('dragend', () => {
            const pos = marker.getLatLng();
            circle.setLatLng(pos);
            dotNetRef.invokeMethodAsync('OnMarkerMoved', pos.lat, pos.lng);
        });

        this._instances[elementId] = { map, marker, circle };
    },

    updateRadius(elementId, radiusKm) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.circle.setRadius(radiusKm * 1000);
        inst.map.fitBounds(inst.circle.getBounds());
    },

    destroy(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.map.remove();
        delete this._instances[elementId];
    }
};
