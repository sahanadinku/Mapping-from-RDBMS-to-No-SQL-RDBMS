using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;

namespace Project
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadDatabases();
        }
        private List<string> databases;
        private List<string> tables;


        private readonly string mssqlConnectionString = "Data Source = LAPTOP-CFH3GB8Q\\SQLEXPRESS; User ID = sahana; Password=123456";
        private readonly string mongoConnectionString = "mongodb://localhost:27017";


        private void LoadDatabases()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(mssqlConnectionString))
                {
                    connection.Open();
                    databases = new List<string>();


                    SqlCommand command = new SqlCommand("SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')", connection);
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        string databaseName = reader.GetString(0);
                        databases.Add(databaseName);
                    }


                    comboBox1.DataSource = databases;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading databases: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadTables(string databaseName)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(mssqlConnectionString))
                {
                    connection.Open();
                    tables = new List<string>();


                    SqlCommand command = new SqlCommand($"USE {databaseName}; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection);
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        string tableName = reader.GetString(0);
                        tables.Add(tableName);
                    }


                    comboBox2.DataSource = tables;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading tables: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedDatabase = comboBox1.SelectedItem.ToString();
            LoadTables(selectedDatabase);
        }

        //Audit 
        public class AuditRecord
        {
            public string DatabaseName { get; set; }
            public string TableName { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime UpdatedDate { get; set; }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string selectedDatabase = comboBox1.SelectedItem.ToString();
            string selectedTable = comboBox2.SelectedItem.ToString();

            try
            {
                using (SqlConnection connection = new SqlConnection(mssqlConnectionString))
                {
                    connection.Open();


                    SqlCommand command = new SqlCommand($"USE {selectedDatabase}; SELECT * FROM {selectedTable}", connection);
                    SqlDataReader reader = command.ExecuteReader();


                    MongoClient client = new MongoClient(mongoConnectionString);
                    IMongoDatabase database = client.GetDatabase(selectedDatabase);
                    IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(selectedTable);


                    collection.DeleteMany(new BsonDocument());


                    while (reader.Read())
                    {
                        BsonDocument document = new BsonDocument();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string fieldName = reader.GetName(i);
                            object value = reader.GetValue(i);
                            document.Add(new BsonElement(fieldName, BsonValue.Create(value)));
                        }

                        collection.InsertOne(document);
                    }

                    MessageBox.Show("Data imported successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    reader.Close();

                    //Intialize the auditRecord object

                    var auditRecord = new AuditRecord
                    {
                        DatabaseName = selectedDatabase,
                        TableName = selectedTable,
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now,

                    };

                    string insertQuery = "INSERT INTO AdventureWorks2022.dbo.AuditTable (DatabaseName, TableName, CreatedDate, UpdatedDate) " +
                    "VALUES (@DatabaseName, @TableName, @CreatedDate, @UpdatedDate)";

                    using (SqlCommand command1 = new SqlCommand(insertQuery, connection))
                    {
                        command1.Parameters.AddWithValue("@DatabaseName", auditRecord.DatabaseName);
                        command1.Parameters.AddWithValue("@TableName", auditRecord.TableName);
                        command1.Parameters.AddWithValue("@CreatedDate", auditRecord.CreatedDate);
                        command1.Parameters.AddWithValue("@UpdatedDate", auditRecord.UpdatedDate);

                        // Execute the INSERT query
                        command1.ExecuteNonQuery();

                    }

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while importing data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void button3_Click(object sender, EventArgs e)
        {
            using (SqlConnection connection = new SqlConnection(mssqlConnectionString))
            {
                connection.Open();
                string selectQuery = $"SELECT * FROM AdventureWorks2022.dbo.AuditTable ORDER BY Id DESC";



                using (SqlCommand command = new SqlCommand(selectQuery, connection))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);



                    dataGridView1.DataSource = dataTable;
                }
            }



        }


        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click_1(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            
                string sqlQuery = richTextBox3.Text;



                // Perform the conversion logic here and return the MongoDB query
                string mongoQuery = "";



                if (sqlQuery.StartsWith("SELECT"))
                {
                    // Conversion logic for SELECT query
                    string tableName = sqlQuery.Substring(sqlQuery.IndexOf("FROM") + 4).Trim();
                    string whereClause = sqlQuery.Contains("WHERE") ? sqlQuery.Substring(sqlQuery.IndexOf("WHERE") + 5).Trim() : "";
                    //mongoQuery = $"db.{tableName}.find({{{whereClause}}})";
                    mongoQuery = $"db.getCollection('{tableName}').find({{{whereClause}}})";
            }
                else if (sqlQuery.StartsWith("UPDATE"))
                {
                // Conversion logic for UPDATE query
                string tableName = sqlQuery.Substring(sqlQuery.IndexOf("UPDATE") + 6, sqlQuery.IndexOf("SET") - 6).Trim();
                string setId = sqlQuery.Contains("WHERE")
                    ? sqlQuery.Substring(sqlQuery.IndexOf("WHERE") + 5).Trim().Split('=')[1].Trim()
                    : "";
                string setClause = sqlQuery.Contains("SET") && sqlQuery.Contains("WHERE")
                    ? $"{{ $set: {{ Name: '{sqlQuery.Substring(sqlQuery.IndexOf("'", sqlQuery.IndexOf("=", StringComparison.Ordinal) + 1) + 1, sqlQuery.LastIndexOf("'", StringComparison.Ordinal) - sqlQuery.IndexOf("'", sqlQuery.IndexOf("=", StringComparison.Ordinal) + 1) - 1)}' }} }}"
                    : "";
                string whereClause = sqlQuery.Contains("WHERE") ? $"{{ ID: {setId} }}" : "";
                mongoQuery = $"db.{tableName}.updateMany({whereClause}, {setClause})";


            }
            else if (sqlQuery.StartsWith("DELETE"))
                {
                               
                string tableNameWithCondition = sqlQuery.Substring(sqlQuery.IndexOf("FROM") + 4).Trim();
                string tableName = tableNameWithCondition.Split(' ')[0];
                string whereClause = sqlQuery.Contains("WHERE") ? tableNameWithCondition.Substring(tableNameWithCondition.IndexOf("WHERE") + 5).Trim() : "";
                mongoQuery = $"db.getCollection('{tableName}').deleteMany({{{whereClause.Split('=')[0].Trim()}: {whereClause.Split('=')[1].Trim()}}})";


            }
            else if (sqlQuery.StartsWith("INSERT"))
                {
                // Conversion logic for INSERT query
                //string tableName = sqlQuery.Substring(sqlQuery.IndexOf("INTO") + 4, sqlQuery.IndexOf("(") - sqlQuery.IndexOf("INTO") - 4).Trim();
                //string columns = sqlQuery.Substring(sqlQuery.IndexOf("(") + 1, sqlQuery.IndexOf(")") - sqlQuery.IndexOf("(") - 1).Trim();
                //string values = sqlQuery.Substring(sqlQuery.IndexOf("VALUES") + 6).Trim();
                //mongoQuery = $"db.{tableName}.insertOne({{ {columns}: {values} }})";
                string tableName = sqlQuery.Substring(sqlQuery.IndexOf("INTO") + 4, sqlQuery.IndexOf("(") - sqlQuery.IndexOf("INTO") - 4).Trim();
                string columns = sqlQuery.Substring(sqlQuery.IndexOf("(") + 1, sqlQuery.IndexOf(")") - sqlQuery.IndexOf("(") - 1).Trim();
                string values = sqlQuery.Substring(sqlQuery.IndexOf("VALUES") + 6).Trim();

                string[] columnsArray = columns.Split(',');
                string[] valuesArray = values.Split(',');

                List<string> keyValuePairs = new List<string>();
                for (int i = 0; i < columnsArray.Length; i++)
                {
                    string key = columnsArray[i].Trim();
                    string value = valuesArray[i].Trim();

                    // Handle parentheses and single quotes for string values
                    if (!value.All(char.IsDigit) && value.StartsWith("'") && value.EndsWith("'"))
                    {
                        value = value.Trim('\'');
                        value = $"\"{value}\"";
                    }
                    else
                    {
                        value = value.Trim('(', ')');
                    }

                    keyValuePairs.Add($"\"{key}\": {value}");
                }

                mongoQuery = $"db.getCollection('{tableName}').insertMany([{{ {string.Join(", ", keyValuePairs)} }}]);";


            }
            else
                {
                    // Unsupported SQL query
                    mongoQuery = "Unsupported SQL query";
                }


                richTextBox4.Text = mongoQuery;
            
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }
    }
}
