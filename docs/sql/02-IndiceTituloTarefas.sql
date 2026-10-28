BEGIN TRANSACTION;
CREATE INDEX [IX_Tarefas_Titulo] ON [Tarefas] ([Titulo]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261028170640_IndiceTituloTarefas', N'10.0.12');

COMMIT;
GO

