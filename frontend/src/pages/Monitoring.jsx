
import { useEffect, useState } from "react";
import { HubConnectionBuilder } from '@microsoft/signalr';
import "../styles/Monitoring.css";

const notificationOptions = [
    { id: "email", label: "E-mail" },
    { id: "teams", label: "Microsoft Teams" },
    { id: "desktop", label: "Powiadomienia na komputerze" },
];

function Monitoring() {
    //Pobieranie kontenerów
    const [containers, setContainers] = useState([]);

    const [isFormOpen, setIsFormOpen] = useState(false);
    const [monitorings, setMonitorings] = useState([]);

    const [name, setName] = useState("");
    const [containerId, setContainerId] = useState("");
    const [keywords, setKeywords] = useState([
        { id: 1, word: "", threshold: 5 },
    ]);
    const [notifications, setNotifications] = useState([]);
    const [alertInterval, setAlertInterval] = useState("12");

    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        const connection = new HubConnectionBuilder()
            .withUrl('http://localhost:5241/containerHub')
            .withAutomaticReconnect()
            .build();
        const handleContainersChanged = async () => {
            try {
                const response = await fetch('http://localhost:5241/api/containers');
            if (!response.ok) {
                throw new Error('Nie udało się pobrać kontenerów.');
            }
            const data = await response.json();
            setContainers(data);
        } catch (error) {
            console.error('Błąd odświeżania kontenerów:', error);
        }
        };
        connection.on('ContainersChanged', () => {
        handleContainersChanged();
        });
        connection.start()
        .then(() => {
            console.log('Połączono z SignalR');
        })
        .catch(error => {
            console.error('Błąd połączenia z SignalR:', error);
        });

        return () => {
            connection.stop();
        };
    }, []);

    useEffect(() => {
        async function loadContainers() {
        try {
            const response = await fetch('http://localhost:5241/api/containers')

            if (!response.ok) {
            throw new Error('Nie udało się pobrać kontenerów')
            }

            const data = await response.json();
            console.log('containerLogs:', JSON.stringify(data.containerLogs));
            setContainers(data)
        } catch (error) {
            console.error('Błąd pobierania kontenerów:', error)
        } finally {
            setIsLoading(false)
        }
        }
        loadContainers()
    }, [])

    useEffect(() => {
        async function loadMonitorings() {
            try {
                const response = await fetch('http://localhost:5241/api/monitoring')

                if (!response.ok) {
                throw new Error('Nie udało się pobrać konfiguracji monitoringów')
                }

                const data = await response.json()
                setMonitorings(data)
            } catch (error) {
                console.error('Błąd pobierania konifguracji monitoringów:', error)
            } finally {
                setIsLoading(false)
            }
        }
        loadMonitorings();
    },[]);



    const resetForm = () => {
        setName("");
        setContainerId("");
        setKeywords([{ id: 1, word: "", threshold: 5 }]);
        setNotifications([]);
        setIsFormOpen(false);
    };

    const addKeyword = () => {
        setKeywords((current) => [
            ...current,
            {
                id: Math.max(0, ...current.map((item) => item.id)) + 1,
                word: "",
                threshold: 5,
            },
        ]);
    };

    const updateKeyword = (id, field, value) => {
        setKeywords((current) =>
            current.map((item) =>
                item.id === id ? { ...item, [field]: value } : item
            )
        );
    };

    const removeKeyword = (id) => {
        setKeywords((current) => current.filter((item) => item.id !== id));
    };

    const toggleNotification = (id) => {
        setNotifications((current) =>
            current.includes(id)
                ? current.filter((item) => item !== id)
                : [...current, id]
        );
    };

    const saveMonitoring = async (event) => {
            event.preventDefault();

            const validKeywords = keywords
                .map((item) => ({
                    ...item,
                    word: item.word.trim(),
                    threshold: Number(item.threshold),
                }))
                .filter((item) => item.word !== "");

            if (!name.trim()) {
                alert("Podaj nazwę monitoringu.");
                return;
            }

            if (!containerId) {
                alert("Wybierz kontener.");
                return;
            }

            if (validKeywords.length === 0) {
                alert("Dodaj przynajmniej jedno słowo kluczowe.");
                return;
            }

            if (
                validKeywords.some(
                    (item) =>
                        !Number.isInteger(item.threshold) ||
                        item.threshold < 1
                )
            ) {
                alert("Każdy próg musi być dodatnią liczbą całkowitą.");
                return;
            }

            if (notifications.length === 0) {
                alert("Wybierz przynajmniej jeden kanał powiadomień.");
                return;
            }

            const container = containers.find(
                (item) => item.id === containerId
            );

            if (!container) {
                alert("Nie znaleziono wybranego kontenera.");
                return;
            }

            const newMonitoring = {
                name: name.trim(),
                containerId,
                containerName: container.name,
                keywordsJson: JSON.stringify(validKeywords),
                notificationsJson: JSON.stringify(notifications),
                alertInterval: Number(alertInterval),
                isEnabled: true,
            };

            try {
                const response = await fetch(
                    "http://localhost:5241/api/monitoring",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify(newMonitoring),
                    }
                );

                if (!response.ok) {
                    throw new Error(
                        "Nie udało się zapisać konfiguracji monitoringu."
                    );
                }

                const savedMonitoring = {
                    ...newMonitoring,
                    Id: null,
                    Name: newMonitoring.name,
                    ContainerId: newMonitoring.containerId,
                    ContainerName: newMonitoring.containerName,
                    KeywordsJson: newMonitoring.keywordsJson,
                    NotificationsJson: newMonitoring.notificationsJson,
                    AlertInterval: newMonitoring.alertInterval,
                    IsEnabled: newMonitoring.isEnabled,
                };

                // Pobierz zapisane dane, aby otrzymać prawdziwe Id z bazy.
                const refreshResponse = await fetch(
                    "http://localhost:5241/api/monitoring"
                );

                if (!refreshResponse.ok) {
                    throw new Error(
                        "Zapisano monitoring, ale nie udało się odświeżyć listy."
                    );
                }

                const data = await refreshResponse.json();
                setMonitorings(data);

                resetForm();
            } catch (error) {
                console.error("Błąd zapisywania monitoringu:", error);
                alert(error.message);
            }
        };

    
        const toggleMonitoring = (id) => {
        setMonitorings((current) =>
            current.map((item) =>
                item.id === id
                    ? { ...item, isActive: !item.isActive }
                    : item
            )
        );
    };

    const deleteMonitoring = (id) => {
        setMonitorings((current) =>
            current.filter((item) => item.id !== id)
        );
    };

    return (
        <main className="monitoring-page">
            <header className="monitoring-header">
                <div>
                </div>

                <button
                    className="monitoring-add-button"
                    onClick={() => setIsFormOpen(true)}
                    disabled={isFormOpen}
                >
                    + Dodaj nowy monitoring
                </button>
            </header>

            {isFormOpen && (
                <form className="monitoring-form" onSubmit={saveMonitoring}>
                    <h2>Nowy monitoring</h2>

                    <div className="monitoring-field">
                        <label htmlFor="monitoring-name">
                            Nazwa monitoringu
                        </label>
                        <input
                            id="monitoring-name"
                            type="text"
                            placeholder="Np. Błędy aplikacji"
                            value={name}
                            onChange={(event) => setName(event.target.value)}
                            required
                        />
                    </div>

                    <div className="monitoring-field">
                        <label htmlFor="monitoring-container">
                            Kontener
                        </label>
                        <select
                            id="monitoring-container"
                            value={containerId}
                            onChange={(event) =>
                                setContainerId(event.target.value)
                            }
                            required
                        >
                            <option value="">Wybierz kontener</option>
                            {containers.map((c) => (
                                <option key={c.id} value={c.id}>
                                    {c.names.join(", ").replaceAll("/", "")}
                                </option>
                            ))}
                        </select>
                    </div>

                    <section className="monitoring-keywords">
                        <div className="monitoring-section-header">
                            <h3>Słowa kluczowe</h3>
                            <button
                                type="button"
                                className="monitoring-secondary-button"
                                onClick={addKeyword}
                            >
                                + Dodaj słowo
                            </button>
                        </div>

                        {keywords.map((keyword, index) => (
                            <div className="monitoring-keyword" key={keyword.id}>
                                <div className="monitoring-keyword-heading">
                                    <span>Słowo {index + 1}</span>
                                    {keywords.length > 1 && (
                                        <button
                                            type="button"
                                            className="monitoring-remove-button"
                                            onClick={() =>
                                                removeKeyword(keyword.id)
                                            }
                                        >
                                            Usuń
                                        </button>
                                    )}
                                </div>

                                <div className="monitoring-keyword-fields">
                                    <div className="monitoring-field">
                                        <label htmlFor={`keyword-${keyword.id}`}>
                                            Słowo lub fraza
                                        </label>
                                        <input
                                            id={`keyword-${keyword.id}`}
                                            type="text"
                                            placeholder="Np. ERROR"
                                            value={keyword.word}
                                            onChange={(event) =>
                                                updateKeyword(
                                                    keyword.id,
                                                    "word",
                                                    event.target.value
                                                )
                                            }
                                        />
                                    </div>

                                    <div className="monitoring-field">
                                        <label htmlFor={`threshold-${keyword.id}`}>
                                            Próg wystąpień
                                        </label>
                                        <input
                                            id={`threshold-${keyword.id}`}
                                            type="number"
                                            min="1"
                                            step="1"
                                            value={keyword.threshold}
                                            onChange={(event) =>
                                                updateKeyword(
                                                    keyword.id,
                                                    "threshold",
                                                    event.target.value
                                                )
                                            }
                                            required
                                        />
                                    </div>
                                </div>
                            </div>
                        ))}
                        <p className="monitoring-hint">
                            Próg oznacza liczbę wystąpień danego słowa,
                            po której ma zostać wygenerowany alert.
                            Dokładny sposób liczenia ustalimy później.
                        </p>
                    </section>

                    <section className="monitoring-notifications">
                        <h3>Kanały powiadomień</h3>

                        {notificationOptions.map((option) => (
                            <label
                                className="monitoring-checkbox"
                                key={option.id}
                            >
                                <input
                                    type="checkbox"
                                    checked={notifications.includes(option.id)}
                                    onChange={() =>
                                        toggleNotification(option.id)
                                    }
                                />
                                <span>{option.label}</span>
                            </label>
                        ))}

                        <p className="monitoring-hint">
                            Wybór kanału zapisuje się w konfiguracji.
                            Wysyłanie powiadomień nie jest jeszcze podłączone.
                        </p>
                    </section>

                    <div className="form-group">
                        <label htmlFor="alertInterval">
                            Częstotliwość powiadomień
                        </label>

                        <select
                            id="alertInterval"
                            value={alertInterval}
                            onChange={(e) => setAlertInterval(e.target.value)}
                        >
                            <option value="1">Co 1 godzinę</option>
                            <option value="12">Co 12 godzin</option>
                            <option value="24">Co 24 godziny</option>
                        </select>

                        <small>
                            Określa, jak często ponawiać alert,
                            gdy próg nadal jest przekroczony.
                        </small>
                    </div>

                    <div className="monitoring-form-actions">
                        <button
                            type="button"
                            className="monitoring-cancel-button"
                            onClick={resetForm}
                        >
                            Anuluj
                        </button>
                        <button
                            type="submit"
                            className="monitoring-add-button"
                        >
                            Zapisz monitoring
                        </button>
                    </div>
                </form>
            )}

            <section className="monitoring-list">
                <h2>Twoje monitoringi</h2>

                {monitorings.length === 0 ? (
                    <div className="monitoring-empty">
                        <p>Nie masz jeszcze skonfigurowanych monitoringów.</p>
                        <span>
                            Kliknij „Dodaj nowy monitoring”, aby utworzyć
                            pierwszą regułę.
                        </span>
                    </div>
                ) : (
                    <div className="monitoring-cards">
                        {monitorings.map((item) => (
                            <article
                                className="monitoring-card"
                                key={item.Id}
                            >
                                <div className="monitoring-card-header">
                                    <div>
                                        <h3>{item.Name}</h3>
                                        <p>
                                            Kontener: {item.ContainerName}
                                        </p>
                                    </div>
                                    <span
                                        className={`monitoring-status ${
                                            item.IsEnabled
                                                ? "is-active"
                                                : "is-inactive"
                                        }`}
                                    >
                                        {item.IsEnabled ? "Aktywny" : "Wyłączony"}
                                    </span>
                                </div>

                                <div className="monitoring-card-keywords">
                                    {item.KeywordsJson.map((keyword) => (
                                        <span
                                            className="monitoring-keyword-tag"
                                            key={keyword.id}
                                        >
                                            {keyword.word} · próg{" "}
                                            {keyword.threshold}
                                        </span>
                                    ))}
                                </div>

                                <p className="monitoring-card-channels">
                                    Powiadomienia:{" "}
                                    {item.NotificationsJson
                                        .map(
                                            (id) =>
                                                notificationOptions.find(
                                                    (option) =>
                                                        option.id === id
                                                )?.label
                                        )
                                        .join(", ")}
                                </p>

                                <div className="monitoring-card-actions">
                                    <button
                                        type="button"
                                        className="monitoring-secondary-button"
                                        onClick={() =>
                                            toggleMonitoring(item.id)
                                        }
                                    >
                                        {item.IsEnabled
                                            ? "Wyłącz monitoring"
                                            : "Włącz monitoring"}
                                    </button>
                                    <button
                                        type="button"
                                        className="monitoring-remove-button"
                                        onClick={() =>
                                            deleteMonitoring(item.Id)
                                        }
                                    >
                                        Usuń
                                    </button>
                                </div>
                            </article>
                        ))}
                    </div>
                )}
            </section>
        </main>
    );
}

export default Monitoring;
