namespace XeSoft.EventStore.Core

// These statements are safe to run on an existing system.
// This is in part because rules specify DO NOTHING INSTEAD.
module DbInit =

    open XeSoft.EventStore.Core.Const

    let getTableNames =
        """
        SELECT c.relname AS TableName
        FROM pg_catalog.pg_class c
        LEFT JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r','')
        AND n.nspname <> 'pg_catalog'
        AND n.nspname <> 'information_schema'
        AND n.nspname !~ '^pg_toast'
        AND pg_catalog.pg_table_is_visible(c.oid)
        ORDER BY 1
        """

    // queries are interpolated only to guarantee (with code)
    // that names match externally used constants
    let event =
        $"""
        CREATE TABLE IF NOT EXISTS {Table.Event}
        (
            Position bigint NOT NULL,
            StreamId uuid NOT NULL,
            Version int NOT NULL,
            Type text NOT NULL,
            Data jsonb,
            Meta jsonb,
            LogDate timestamptz NOT NULL DEFAULT now(),
            CONSTRAINT pk_{Table.Event}_position PRIMARY KEY (Position),
            CONSTRAINT {Constraint.StreamIdVersion} UNIQUE (StreamId, Version)
        );

        -- Append only
        CREATE OR REPLACE RULE rule_{Table.Event}_nodelete AS
        ON DELETE TO {Table.Event} DO INSTEAD NOTHING;
        CREATE OR REPLACE RULE rule_{Table.Event}_noupdate AS
        ON UPDATE TO {Table.Event} DO INSTEAD NOTHING;
        """

    let stream =
        $"""
        CREATE TABLE IF NOT EXISTS {Table.Stream}
        (
            StreamId uuid NOT NULL,
            Meta jsonb,
            CONSTRAINT pk_{Table.Stream}_streamid PRIMARY KEY (StreamId)
        );
        """

    let eventNotification =
        $"""
        DROP TRIGGER IF EXISTS trg_NotifyEvent ON {Table.Event};
        DROP FUNCTION IF EXISTS NotifyEvent();

        CREATE FUNCTION NotifyEvent() RETURNS trigger AS $$

            DECLARE
                payload text;

            BEGIN
                -- < position >/< stream id >/< version >/< event type >
                SELECT CONCAT_WS( '/'
                                , NEW.Position
                                , REPLACE(CAST(NEW.StreamId AS text), '-', '')
                                , NEW.Version
                                , NEW.Type
                                )
                  INTO payload
                ;

                -- using lower case channel name or else LISTEN would require quoted identifier.
                PERFORM pg_notify('{Channel.EventRecorded}', payload);

                RETURN NULL;

            END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER trg_NotifyEvent
            AFTER INSERT ON {Table.Event}
            FOR EACH ROW
            EXECUTE PROCEDURE NotifyEvent()
        ;
        """


    let positionCounter =
        $"""
        CREATE TABLE IF NOT EXISTS {Table.PositionCounter}
        (
            Position bigint NOT NULL
        );

        -- initialize the value
        INSERT INTO {Table.PositionCounter} VALUES (0);

        -- prevent duplication on reinitialization
        CREATE OR REPLACE RULE rule_{Table.PositionCounter}_noinsert AS
        ON INSERT TO {Table.PositionCounter} DO INSTEAD NOTHING;
        -- prevent accidental deletion
        CREATE OR REPLACE RULE rule_{Table.PositionCounter}_nodelete AS
        ON DELETE TO {Table.PositionCounter} DO INSTEAD NOTHING;

        -- create function to increment/return position
        DROP FUNCTION IF EXISTS NextPosition();
        CREATE FUNCTION NextPosition() RETURNS bigint AS $$
            DECLARE
                nextPos bigint;
            BEGIN
                UPDATE {Table.PositionCounter}
                   SET Position = Position + 1
                ;
                SELECT INTO nextPos Position FROM {Table.PositionCounter};
                RETURN nextPos;
            END;
        $$ LANGUAGE plpgsql;
        """


    let getPositions =
        $"""
        SELECT (SELECT COALESCE(MAX(Position), 0) FROM {Table.Event}) AS Event
             , (SELECT Position FROM {Table.PositionCounter}) AS Counter
        """


    let syncPosition =
        $"""
        UPDATE {Table.PositionCounter}
           SET Position = (SELECT COALESCE(MAX(Position), 0) FROM {Table.Event})
        ;
        """
