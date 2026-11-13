namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;

/// <summary>SQL statements and query templates for ScheduledTask storage.</summary>
public static class ScheduledTaskDbSql
{
    /// <summary>SQL for catalog.</summary>
    internal const string Catalog =
        """
        SELECT payload FROM scheduled_task.scheduled_task_catalog WHERE environment=$1 AND host_id=$2;
        """;

    /// <summary>SQL for completed date.</summary>
    internal const string CompletedDate =
        """
        SELECT completed_value_date FROM scheduled_task.futures_completed_end_of_day WHERE environment=$1;
        """;

    /// <summary>SQL for definition.</summary>
    internal const string Definition =
        """
        SELECT payload
        FROM scheduled_task.scheduled_task_definition WHERE environment=$1 AND host_id=$2 AND schedule_id=$3;
        """;

    /// <summary>SQL for definitions.</summary>
    internal const string Definitions =
        """
        SELECT payload
        FROM scheduled_task.scheduled_task_definition
        WHERE environment=$1 AND host_id=$2 ORDER BY schedule_id LIMIT $3;
        """;

    /// <summary>SQL for history.</summary>
    internal const string History =
        """
        SELECT payload
        FROM scheduled_task.scheduled_task_run
        WHERE environment=$1 AND host_id=$2 AND schedule_id=$3 ORDER BY intended_fire DESC,run_id ASC LIMIT $4;
        """;

    /// <summary>SQL for history after.</summary>
    internal const string HistoryAfter =
        """
        SELECT payload
        FROM (
            (SELECT payload,intended_fire,run_id
             FROM scheduled_task.scheduled_task_run
             WHERE environment=$1 AND host_id=$2 AND schedule_id=$3 AND intended_fire=$4 AND run_id>$5
             ORDER BY intended_fire DESC,run_id ASC LIMIT $6)
            UNION ALL
            (SELECT payload,intended_fire,run_id
             FROM scheduled_task.scheduled_task_run
             WHERE environment=$1 AND host_id=$2 AND schedule_id=$3 AND intended_fire<$4
             ORDER BY intended_fire DESC,run_id ASC LIMIT $6)
        ) page
        ORDER BY intended_fire DESC,run_id ASC LIMIT $6;
        """;

    /// <summary>SQL for run.</summary>
    internal const string Run =
        """
        SELECT payload
        FROM scheduled_task.scheduled_task_run
        WHERE environment=$1 AND host_id=$2 AND schedule_id=$3 AND intended_fire=$4 AND run_id=$5;
        """;

    /// <summary>SQL for runs.</summary>
    internal const string Runs =
        """
        SELECT payload
        FROM scheduled_task.scheduled_task_run
        WHERE environment=$1 AND host_id=$2 AND schedule_id=$3 ORDER BY intended_fire DESC,run_id ASC LIMIT $4;
        """;

    /// <summary>SQL for upsert catalog.</summary>
    internal const string UpsertCatalog =
        """
        INSERT INTO scheduled_task.scheduled_task_catalog(environment,host_id,revision,payload)
        VALUES($1,$2,$3,$4)
        ON CONFLICT(environment,host_id) DO UPDATE SET revision=EXCLUDED.revision,payload=EXCLUDED.payload
        WHERE scheduled_task_catalog.revision <= EXCLUDED.revision;
        """;

    /// <summary>SQL for upsert completed date.</summary>
    internal const string UpsertCompletedDate =
        """
        INSERT INTO scheduled_task.futures_completed_end_of_day(environment,completed_value_date,run_id)
        VALUES($1,$2,$3)
        ON CONFLICT(environment) DO UPDATE SET completed_value_date=EXCLUDED.completed_value_date,run_id=EXCLUDED.run_id
        WHERE futures_completed_end_of_day.completed_value_date <= EXCLUDED.completed_value_date;
        """;

    /// <summary>SQL for upsert definition.</summary>
    internal const string UpsertDefinition =
        """
        INSERT INTO scheduled_task.scheduled_task_definition(environment,host_id,schedule_id,revision,payload)
        VALUES($1,$2,$3,$4,$5)
        ON CONFLICT(environment,host_id,schedule_id) DO UPDATE SET revision=EXCLUDED.revision,payload=EXCLUDED.payload
        WHERE scheduled_task_definition.revision <= EXCLUDED.revision;
        """;

    /// <summary>SQL for upsert run.</summary>
    internal const string UpsertRun =
        """
        INSERT INTO scheduled_task.scheduled_task_run(environment,host_id,schedule_id,intended_fire,run_id,revision,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7)
        ON CONFLICT(environment,host_id,schedule_id,intended_fire,run_id) DO UPDATE SET revision=EXCLUDED.revision,
            payload=EXCLUDED.payload
        WHERE scheduled_task_run.revision <= EXCLUDED.revision;
        """;
}
